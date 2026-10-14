using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class OcrProcess
    {
        internal static async Task<string> RunAsync(string executable, string path, int maximumSide,
            CancellationToken token)
        {
            var start = new ProcessStartInfo
            {
                FileName = executable, WorkingDirectory = Path.GetDirectoryName(executable),
                Arguments = "--ensureAscii=1 --models=models --det=ch_PP-OCRv4_det_infer.onnx"
                    + " --rec=rec_ch_PP-OCRv4_infer.onnx --keys=dict_chinese.txt"
                    + " --doAngle=0 --mostAngle=0 --numThread=2 --maxSideLen=" + maximumSide,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            token.ThrowIfCancellationRequested();
            using (var process = Process.Start(start))
            using (var watchdog = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task<string> errors = process.StandardError.ReadToEndAsync();
                Task exited = Task.Run((Action)process.WaitForExit);
                Task<string> reply = ExchangeAsync(process, path);
                Task deadline = Task.Delay(TimeSpan.FromSeconds(20), watchdog.Token);
                Task completed = await Task.WhenAny(reply, deadline).ConfigureAwait(false);
                Stop(process);
                await exited.ConfigureAwait(false);
                await errors.ConfigureAwait(false);
                if (completed != reply)
                {
                    try { await reply.ConfigureAwait(false); } catch (IOException) { } catch (InvalidOperationException) { }
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("文字识别超时，请缩小截图区域后重试。");
                }
                watchdog.Cancel();
                token.ThrowIfCancellationRequested();
                return await reply.ConfigureAwait(false);
            }
        }
        private static async Task<string> ExchangeAsync(Process process, string path)
        {
            string line;
            int count = 0;
            while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (++count > 32) throw InvalidWorker();
                if (line.Trim() == "OCR init completed.") break;
            }
            if (line == null) throw InvalidWorker();
            string json = new JavaScriptSerializer().Serialize(new { image_path = path });
            var ascii = new StringBuilder();
            foreach (char value in json)
                ascii.Append(value > 127 ? "\\u" + ((int)value).ToString("x4") : value.ToString());
            await process.StandardInput.WriteLineAsync(ascii.ToString()).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
            if (line == null || !line.TrimStart().StartsWith("{", StringComparison.Ordinal)) throw InvalidWorker();
            return line;
        }
        private static InvalidOperationException InvalidWorker()
        { return new InvalidOperationException("离线文字识别组件返回异常，请重新解压完整离线包。"); }
        private static void Stop(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
        }
    }
}
