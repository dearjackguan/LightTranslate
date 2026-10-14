using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightTranslate
{
    internal sealed class AppController : IDisposable
    {
        private readonly MainForm form;
        private readonly TranslationService translator = new TranslationService();
        private readonly OcrService ocr = new OcrService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp"));
        private Task activeTask;
        private CancellationTokenSource cancellation;
        private bool closing;

        internal AppController(MainForm form)
        {
            this.form = form;
            form.TranslationRequested += TranslateInput;
            form.ScreenshotRequested += TranslateScreenshot;
            form.CancellationRequested += Cancel;
            form.Shown += OnShown;
            form.FormClosing += OnClosing;
        }

        private void OnShown(object sender, EventArgs e)
        {
            form.SetStatus(form.HotkeyRegistered ? "就绪 · Ctrl+Alt+S 截图，Ctrl+Enter 翻译"
                : "截图快捷键已被占用，请使用截图翻译按钮。", !form.HotkeyRegistered);
        }

        private void TranslateInput() { StartOperation(TranslateInputAsync); }
        private void TranslateScreenshot() { StartOperation(TranslateScreenshotAsync); }

        private void StartOperation(Func<CancellationToken, Task> operation)
        {
            if (activeTask != null && !activeTask.IsCompleted) return;
            cancellation = new CancellationTokenSource();
            form.SetBusy(true);
            activeTask = RunAsync(operation, cancellation);
        }

        private async Task RunAsync(Func<CancellationToken, Task> operation, CancellationTokenSource source)
        {
            try { await operation(source.Token); }
            catch (OperationCanceledException)
            {
                form.SetStatus(source.IsCancellationRequested ? "已取消" : "本地操作超时，请重试。", !source.IsCancellationRequested);
            }
            catch (Win32Exception) { form.SetStatus("无法启动本地组件，请检查 OCR 和离线引擎安装。", true); }
            catch (InvalidDataException error) { form.SetStatus(error.Message, true); }
            catch (IOException) { form.SetStatus("本地文件处理失败，请检查引擎文件和目录权限。", true); }
            catch (Exception error) { form.SetStatus(error.Message, true); }
            finally
            {
                if (!form.Visible) form.Show();
                form.SetBusy(false);
                source.Dispose();
                if (cancellation == source) cancellation = null;
            }
        }

        private async Task TranslateInputAsync(CancellationToken token)
        {
            string text = form.SourceText;
            TranslationService.ValidateInput(text);
            await TranslateTextAsync(text, token);
        }

        private async Task TranslateScreenshotAsync(CancellationToken token)
        {
            form.SetStatus("请选择截图区域");
            form.Hide();
            await Task.Delay(120, token);
            using (var image = CaptureService.CaptureRegion())
            {
                form.Show();
                form.Activate();
                if (image == null) { form.SetStatus("已取消截图"); return; }
                form.SetStatus("正在识别文字…");
                OcrRecognition recognition = await ocr.RecognizeDetailedAsync(image, token);
                await ApplyRecognitionAsync(recognition, token);
            }
        }

        internal async Task ApplyRecognitionAsync(OcrRecognition recognition, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            form.SourceText = recognition.Text;
            if (recognition.NeedsReview)
            {
                form.SetStatus("文字不够清晰，请核对原文后点击翻译。", true);
                return;
            }
            await TranslateTextAsync(recognition.Text, token);
        }

        private async Task TranslateTextAsync(string text, CancellationToken token)
        {
            form.SetStatus("正在翻译…");
            TranslationResult translated = await translator.TranslateWithDirectionAsync(text, form.TargetLanguage, token);
            token.ThrowIfCancellationRequested();
            form.SetResult(translated.Text);
            string direction = translated.Source == "none" ? "已保留地址、代码或数字"
                : translated.Source == "zh" ? "中译英" : "英译中";
            form.SetStatus("翻译完成 · " + direction + " · " + text.Length + " 字符");
        }

        private void Cancel()
        {
            if (cancellation != null && !cancellation.IsCancellationRequested)
            {
                form.SetStatus("正在取消…");
                cancellation.Cancel();
            }
        }

        private async void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (activeTask == null || activeTask.IsCompleted) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            Cancel();
            await activeTask;
            form.Close();
        }

        public void Dispose()
        {
            Cancel();
            translator.Dispose();
            form.TranslationRequested -= TranslateInput;
            form.ScreenshotRequested -= TranslateScreenshot;
            form.CancellationRequested -= Cancel;
            form.Shown -= OnShown;
            form.FormClosing -= OnClosing;
        }
    }
}
