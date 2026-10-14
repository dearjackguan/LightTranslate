using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LightTranslate
{
    internal static class AuditVerification
    {
        private const string DirectoryPath = @"A:\Agent\Codex\Translation\audit";
        private const string EnginePath = DirectoryPath + @"\process-probe\engine";
        private static readonly List<object> results = new List<object>();
        private static int failures;

        [STAThread]
        internal static int Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var fixture = new Form { Text = "Translation completeness audit", ShowInTaskbar = false })
            {
                fixture.Shown += async delegate
                {
                    await Check("process-unicode-json-roundtrip-no-model", UnicodeRoundtrip);
                    await Check("process-short-input-cancellation", () => CancellationProbe("Hello"));
                    await Check("process-long-input-cancellation", () => CancellationProbe(new string('中', 5000)));
                    await Check("process-cancellation-after-stdin-read", () => CancellationProbe("Hello", true));
                    await Check("ui-repeated-error-and-input-preservation", RepeatedErrors);
                    await Check("ui-original-result-on-translation-failure", ResultPreservation);
                    await Check("ui-previous-result-marked-after-input-or-direction-change", ResultAfterEdits);
                    await Check("ui-default-and-minimum-size-layout", CheckLayout);
                    await Check("ui-hotkey-conflict-fallback", HotkeyConflict);
                    await Check("ui-keyboard-input-and-copy-fixture", KeyboardAndCopy);
                    await Check("ocr-oversize-feedback-and-cleanup", OversizeOcr);
                    File.WriteAllText(Path.Combine(DirectoryPath, "audit-probes.json"),
                        new JavaScriptSerializer().Serialize(new { results = results, failures = failures,
                            modelInferenceTested = false, utc = DateTime.UtcNow.ToString("o") }), Encoding.UTF8);
                    fixture.Close();
                };
                Application.Run(fixture);
            }
            return failures == 0 ? 0 : 1;
        }

        private static void WriteWorker(string script)
        {
            File.WriteAllText(Path.Combine(EnginePath, "translation-worker.py"), script, new UTF8Encoding(false));
        }

        private static async Task UnicodeRoundtrip()
        {
            WriteWorker("import json,sys\nsys.stdout.reconfigure(encoding='utf-8')\nr=json.loads(sys.stdin.read())\nprint(json.dumps({'ok':True,'text':r['text']},ensure_ascii=False))\n");
            using (var service = new TranslationService(EnginePath))
            {
                string text = "你好 world\n猫🐈 café";
                string actual = await service.TranslateAsync(text, "en", CancellationToken.None);
                Assert(actual == text, "Unicode or line breaks changed across the process pipe.");
            }
        }

        private static async Task CancellationProbe(string text, bool readFirst = false)
        {
            string pidPath = Path.Combine(EnginePath, "worker.pid");
            if (File.Exists(pidPath)) File.Delete(pidPath);
            WriteWorker("import os,time,sys\nfrom pathlib import Path\n" + (readFirst ? "sys.stdin.read()\n" : "")
                + "Path(__file__).with_name('worker.pid').write_text(str(os.getpid()),encoding='utf-8')\ntime.sleep(30)\n");
            using (var service = new TranslationService(EnginePath))
            using (var cancellation = new CancellationTokenSource())
            {
                Task<string> operation = service.TranslateAsync(text, "en", cancellation.Token);
                for (int i = 0; i < 40 && !File.Exists(pidPath); i++) await Task.Delay(25);
                Assert(File.Exists(pidPath), "The probe worker did not start.");
                var timer = Stopwatch.StartNew();
                cancellation.Cancel();
                bool finished = await Task.WhenAny(operation, Task.Delay(2000)) == operation;
                // Terminate only this probe's process after a stalled cancellation.
                if (!finished)
                {
                    int pid = int.Parse(File.ReadAllText(pidPath, Encoding.UTF8));
                    using (var process = Process.GetProcessById(pid))
                    {
                        Assert(string.Equals(process.MainModule.FileName, Path.Combine(EnginePath, "python", "python.exe"),
                            StringComparison.OrdinalIgnoreCase), "Probe process path does not match.");
                        process.Kill();
                        process.WaitForExit();
                    }
                }
                try { await operation; }
                catch (OperationCanceledException) { }
                catch (IOException) { if (finished) throw; }
                Assert(finished, "Cancellation remained blocked for over 2000 ms while writing stdin.");
                Assert(timer.ElapsedMilliseconds < 2000, "Cancellation exceeded its bound.");
            }
        }

        private static async Task RepeatedErrors()
        {
            using (var form = new MainForm())
            using (var controller = new AppController(form))
            {
                form.Show();
                form.SourceText = "Hello world";
                for (int i = 0; i < 3; i++)
                {
                    Find<Button>(form, "TranslateButton").PerformClick();
                    await Task.Delay(20);
                    Assert(form.SourceText == "Hello world", "Error replaced the original input.");
                    Assert(Find<Button>(form, "TranslateButton").Enabled, "UI stayed busy after a failed translation.");
                }
                form.Close();
            }
        }

        private static async Task ResultPreservation()
        {
            using (var form = new MainForm())
            using (var controller = new AppController(form))
            {
                form.Show();
                form.SetResult("Previous completed translation");
                form.SourceText = "Hello world";
                Find<Button>(form, "TranslateButton").PerformClick();
                await Task.Delay(20);
                Assert(form.ResultText == "Previous completed translation", "A failed new translation erased the previous result.");
                Assert(Find<Label>(form, "ResultLabel").Text.Contains("上次结果"), "The retained result was not identified as previous output.");
                form.Close();
            }
        }

        private static Task CheckLayout()
        {
            using (var form = new MainForm())
            {
                form.Show();
                uint nativeDpi = GetDpiForWindow(form.Handle);
                Console.WriteLine("DPI native=" + nativeDpi + " WinForms=" + form.DeviceDpi);
                Assert(form.DeviceDpi == nativeDpi, "WinForms DPI does not match the native window DPI.");
                foreach (Size size in new[] { form.Size, form.MinimumSize })
                {
                    form.Size = size;
                    form.PerformLayout();
                    foreach (Control control in Descendants(form).Where(c => c.Visible && (c is Button || c is ComboBox || c is TextBox)))
                    {
                        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
                        Rectangle parent = control.Parent.RectangleToScreen(control.Parent.ClientRectangle);
                        Assert(parent.Contains(bounds), "Control is clipped at " + size + ": " + control.Name);
                    }
                }
                form.Close();
            }
            return Task.FromResult(0);
        }

        private static Task ResultAfterEdits()
        {
            using (var form = new MainForm())
            {
                form.SourceText = "Hello";
                form.SetResult("Previous translation");
                Assert(Find<Label>(form, "ResultLabel").Text == "译文", "A completed result was marked stale.");
                form.SourceText = "Different input";
                Assert(Find<Label>(form, "ResultLabel").Text.Contains("上次结果"), "Changed input still appears to match the old result.");
                Assert(form.ResultText == "Previous translation", "Editing removed the previous result.");
                form.SetResult("New completed translation");
                Find<ComboBox>(form, "TargetLanguage").SelectedIndex = 1;
                Assert(Find<Label>(form, "ResultLabel").Text.Contains("上次结果"), "Changed direction still appears to match the old result.");
                form.Show();
                var originalClipboard = Clipboard.GetDataObject();
                try
                {
                    Find<Button>(form, "CopyButton").PerformClick();
                    Assert(Find<Label>(form, "StatusText").Text == "上次译文已复制",
                        "Copy feedback presents a stale translation as current.");
                }
                finally
                {
                    if (originalClipboard == null) Clipboard.Clear();
                    else Clipboard.SetDataObject(originalClipboard, true);
                }
            }
            return Task.FromResult(0);
        }

        private static async Task HotkeyConflict()
        {
            using (var first = new MainForm())
            using (var second = new MainForm())
            using (var controller = new AppController(second))
            {
                first.Show();
                second.Show();
                await Task.Delay(30);
                Assert(first.HotkeyRegistered && !second.HotkeyRegistered, "Shortcut conflict was not detected.");
                Assert(Find<Label>(second, "StatusText").Text.Contains("占用"), "Conflict fallback message is absent.");
                Assert(Find<Button>(second, "ScreenshotButton").Enabled, "Screenshot button was disabled on hotkey conflict.");
                first.Close();
                second.Close();
            }
        }

        private static async Task KeyboardAndCopy()
        {
            IDataObject originalClipboard = Clipboard.GetDataObject();
            try
            {
                using (var form = new MainForm())
                using (var controller = new AppController(form))
                {
                    form.Show();
                    await Task.Delay(30);
                    object[] arguments = { new Message(), Keys.Control | Keys.Enter };
                    var method = typeof(MainForm).GetMethod("ProcessCmdKey",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Assert((bool)method.Invoke(form, arguments), "Ctrl+Enter was not handled.");
                    Assert(Find<Label>(form, "StatusText").Text.StartsWith("请输入"), "Ctrl+Enter did not invoke translation validation.");
                    const string text = "Audit clipboard fixture: 中文 123";
                    form.SetResult(text);
                    Find<Button>(form, "CopyButton").PerformClick();
                    Assert(Clipboard.GetText() == text, "Copy button did not preserve fixture text.");
                    form.Close();
                }
            }
            finally
            {
                if (originalClipboard == null) Clipboard.Clear();
                else Clipboard.SetDataObject(originalClipboard, true);
            }
        }

        private static async Task OversizeOcr()
        {
            string directory = Path.Combine(DirectoryPath, "oversize-ocr");
            using (var image = new Bitmap(10001, 5))
            {
                try
                {
                    await new OcrService(directory, @"A:\Agent\Codex\Translation\app\ocr").RecognizeAsync(image, CancellationToken.None);
                    throw new InvalidOperationException("Oversize OCR unexpectedly succeeded.");
                }
                catch (InvalidOperationException error)
                {
                    Assert(error.Message.Contains("尺寸限制"), "Oversize OCR did not return the expected visible error.");
                }
            }
            Assert(!Directory.GetFiles(directory).Any(), "Oversize OCR left temporary files.");
        }

        private static T Find<T>(Control root, string name) where T : Control
        { return Descendants(root).OfType<T>().First(c => c.Name == name); }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in Descendants(child)) yield return nested;
            }
        }

        private static async Task Check(string name, Func<Task> action)
        {
            try
            {
                await action();
                results.Add(new { name = name, status = "pass" });
                Console.WriteLine("PASS " + name);
            }
            catch (Exception error)
            {
                failures++;
                results.Add(new { name = name, status = "fail", detail = error.Message });
                Console.WriteLine("FAIL " + name + ": " + error.Message);
            }
        }

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);
    }
}
