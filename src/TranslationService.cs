using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal sealed class TranslationService : IDisposable
    {
        internal const int MaximumCharacters = 5000;
        private readonly string engineDirectory;

        internal TranslationService() : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine")) { }
        internal TranslationService(string engineDirectory) { this.engineDirectory = engineDirectory; }

        internal async Task<string> TranslateAsync(string text, string target, CancellationToken token)
        {
            return (await TranslateWithDirectionAsync(text, target, token).ConfigureAwait(false)).Text;
        }

        internal async Task<TranslationResult> TranslateWithDirectionAsync(string text, string target, CancellationToken token)
        {
            ValidateInput(text);
            if (target != "auto" && target != "zh-CN" && target != "en")
                throw new ArgumentException("请选择自动识别、英译中或中译英。");
            token.ThrowIfCancellationRequested();
            string python = Path.Combine(engineDirectory, "python", "python.exe");
            string worker = Path.Combine(engineDirectory, "translation-worker.py");
            if (!File.Exists(python) || !File.Exists(worker))
                throw new InvalidOperationException("离线翻译引擎尚未安装，请完成本地引擎部署。");
            var start = new ProcessStartInfo
            {
                FileName = python, Arguments = "-I -B \"" + worker + "\"",
                WorkingDirectory = engineDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start))
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromMinutes(3));
                using (deadline.Token.Register(delegate { StopWorker(process); }))
                    return await ExchangeAsync(process, text, target, deadline, token).ConfigureAwait(false);
            }
        }

        private static async Task<TranslationResult> ExchangeAsync(Process process, string text, string target,
            CancellationTokenSource deadline, CancellationToken token)
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            Task exited = Task.Run((Action)process.WaitForExit);
            Exception failure = null;
            string response = null;
            try
            {
                var request = new { text = text, source = target == "auto" ? "auto" : target == "en" ? "zh" : "en",
                    target = target == "auto" ? "auto" : target == "en" ? "en" : "zh" };
                // ASCII JSON escapes avoid dependence on the Windows pipe code page.
                string json = new JavaScriptSerializer().Serialize(request);
                var ascii = new StringBuilder();
                foreach (char c in json) ascii.Append(c > 127 ? "\\u" + ((int)c).ToString("x4") : c.ToString());
                await process.StandardInput.WriteAsync(ascii.ToString()).ConfigureAwait(false);
                process.StandardInput.Close();
                await exited.ConfigureAwait(false);
                response = await output.ConfigureAwait(false);
                await error.ConfigureAwait(false);
                ThrowIfStopped(deadline, token);
            }
            catch (IOException exception)
            {
                failure = token.IsCancellationRequested ? (Exception)new OperationCanceledException(token)
                    : deadline.IsCancellationRequested ? (Exception)new TimeoutException("离线翻译超时，请缩短原文后重试。") : exception;
            }
            catch (Exception exception) { failure = exception; }
            // C# 5 cannot await in finally; preserve failures across asynchronous cleanup.
            StopWorker(process);
            await exited.ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            TranslationResult parsed = ParseDetailedResponse(response);
            if (parsed.Source != null) return parsed;
            if (target == "auto") throw InvalidResponse();
            return new TranslationResult(parsed.Text, target == "en" ? "zh" : "en", target == "en" ? "en" : "zh");
        }

        private static void ThrowIfStopped(CancellationTokenSource deadline, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested)
                throw new TimeoutException("离线翻译超时，请缩短原文后重试。");
        }

        private static void StopWorker(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
        }

        internal static void ValidateInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("请输入文字，或截图选择包含文字的区域。");
            if (text.Length > MaximumCharacters)
                throw new ArgumentException("单次最多翻译 5000 字符，请缩短原文后重试。");
        }

        internal static string ParseResponse(string json)
        {
            return ParseDetailedResponse(json).Text;
        }

        internal static TranslationResult ParseDetailedResponse(string json)
        {
            Dictionary<string, object> result;
            try { result = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>; }
            catch (ArgumentException) { throw InvalidResponse(); }
            if (result == null || !result.ContainsKey("ok") || !(result["ok"] is bool)) throw InvalidResponse();
            if (!(bool)result["ok"])
            {
                string code = result.ContainsKey("error") ? result["error"] as string : null;
                if (code == "MODEL_MISSING") throw new InvalidOperationException("对应的离线翻译模型尚未安装。");
                if (code == "DEPENDENCY_MISSING") throw new InvalidOperationException("离线推理组件不完整，请重新部署引擎。");
                if (code == "OUTPUT_LIMIT") throw new InvalidOperationException("译文超过本地模型的长度限制，请分段翻译后重试。");
                if (code == "PROTECTED_TEXT_CHANGED") throw new InvalidOperationException("译文中的地址、代码或金额未能完整保留，请缩小翻译范围后重试。");
                if (code == "LANGUAGE_UNSUPPORTED") throw new InvalidOperationException("未识别到中英文正文，目前仅支持中文和英文。");
                if (code == "QUALITY_CHECK_FAILED") throw new InvalidOperationException("本地模型未通过语义检查，请拆分或改写句子后重试。");
                throw new InvalidOperationException("离线翻译失败，请检查本地模型后重试。");
            }
            string text = result.ContainsKey("text") ? result["text"] as string : null;
            if (string.IsNullOrWhiteSpace(text)) throw InvalidResponse();
            string source = result.ContainsKey("source") ? result["source"] as string : null;
            string target = result.ContainsKey("target") ? result["target"] as string : null;
            if (result.ContainsKey("source") || result.ContainsKey("target"))
            {
                bool direction = source == "en" && target == "zh" || source == "zh" && target == "en"
                    || source == "none" && target == "none";
                if (!direction) throw InvalidResponse();
            }
            return new TranslationResult(text.Trim(), source, target);
        }

        private static InvalidDataException InvalidResponse()
        { return new InvalidDataException("离线翻译引擎返回了无效结果。"); }

        public void Dispose() { }
    }
}
