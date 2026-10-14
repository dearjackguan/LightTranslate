using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class ServiceVerification
    {
        private static readonly List<string> passed = new List<string>();
        internal static readonly string ArtifactDirectory = @"A:\Agent\Codex\Translation\verification";

        internal static async Task RunAsync(bool includeTranslation)
        {
            await Check("offline-response-preserves-lines", delegate
            {
                string text = TranslationService.ParseResponse("{\"ok\":true,\"text\":\"First line\\nSecond line\"}");
                Assert(text == "First line\nSecond line", "Translation lines were not preserved.");
                return Task.FromResult(0);
            });
            await Check("malformed-offline-response-rejected", async delegate
            {
                foreach (string json in new[] { "{}", "not-json", "{\"ok\":null}", "{\"ok\":true,\"text\":123}", "{\"ok\":true,\"text\":\" \"}",
                    "{\"ok\":true,\"text\":\"Hello\",\"source\":\"fr\",\"target\":\"en\"}",
                    "{\"ok\":true,\"text\":\"Hello\",\"source\":\"zh\"}" })
                    await Throws<InvalidDataException>(() => Task.FromResult(TranslationService.ParseResponse(json)));
            });
            await Check("offline-worker-errors-visible", async delegate
            {
                foreach (string code in new[] { "MODEL_MISSING", "DEPENDENCY_MISSING", "ENGINE_FAILURE" })
                    await Throws<InvalidOperationException>(() => Task.FromResult(TranslationService.ParseResponse(
                        "{\"ok\":false,\"error\":\"" + code + "\"}")));
                foreach (string code in new[] { "OUTPUT_LIMIT", "PROTECTED_TEXT_CHANGED", "QUALITY_CHECK_FAILED" })
                {
                    try { TranslationService.ParseResponse("{\"ok\":false,\"error\":\"" + code + "\"}"); }
                    catch (InvalidOperationException error)
                    {
                        Assert(error.Message.Contains("重试") && !error.Message.Contains("检查本地模型"),
                            "A recoverable length/identifier error only suggests reinstalling the model.");
                        continue;
                    }
                    throw new InvalidOperationException("The worker failure was accepted as a result.");
                }
            });
            await VerifyProviderFailures();
            await VerifyGeometry();
            await VerifyOcr();
            if (includeTranslation)
            {
            await Check("offline-model-english-to-chinese", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\app\engine"))
                {
                    string result = await service.TranslateAsync("Hello world", "zh-CN", CancellationToken.None);
                    bool greeting = new[] { "你好", "您好", "哈罗", "哈啰" }.Any(result.Contains);
                    Assert(greeting && result.Contains("世界"), "Unexpected English-to-Chinese meaning.");
                }
            });
            await Check("offline-model-chinese-to-english", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\app\engine"))
                {
                    string result = await service.TranslateAsync("你好世界", "en", CancellationToken.None);
                    Assert(result.ToLowerInvariant().Contains("hello") && result.ToLowerInvariant().Contains("world"), "Unexpected Chinese-to-English result.");
                }
            });
            await Check("offline-model-automatic-direction-and-literal-only-input", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\app\engine"))
                {
                    TranslationResult english = await service.TranslateWithDirectionAsync("Hello world", "auto", CancellationToken.None);
                    Assert(english.Source == "en" && english.Target == "zh" && english.Text.Contains("世界"), "English auto direction failed.");
                    TranslationResult chinese = await service.TranslateWithDirectionAsync("请打开翻译窗口。", "auto", CancellationToken.None);
                    Assert(chinese.Source == "zh" && chinese.Target == "en" && chinese.Text.ToLowerInvariant().Contains("window"), "Chinese auto direction failed.");
                    TranslationResult literal = await service.TranslateWithDirectionAsync("support@example.com", "auto", CancellationToken.None);
                    Assert(literal.Source == "none" && literal.Target == "none" && literal.Text == "support@example.com", "Literal-only auto input was altered.");
                }
            });
            await Check("offline-model-scoped-semantic-decoding", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\app\engine"))
                {
                    string tax = (await service.TranslateAsync("每月订阅费用为19.99美元，不含税。", "auto", CancellationToken.None)).ToLowerInvariant();
                    Assert(tax.Contains("19.99") && Regex.IsMatch(tax, @"without tax|exclud|not (include|contain)") && !tax.Contains("free"), "Tax exclusion became tax exemption.");
                    string power = (await service.TranslateAsync("更新安装期间，请勿关闭计算机。", "auto", CancellationToken.None)).ToLowerInvariant();
                    Assert(Regex.IsMatch(power, @"(not|don't).*?(turn off|shut down)") && !power.Contains("close"), "A power action became a window action.");
                }
            });
            }
            File.WriteAllText(Path.Combine(ArtifactDirectory, includeTranslation ? "service-results.json" : "local-service-results.json"),
                new JavaScriptSerializer().Serialize(new { passed = passed, count = passed.Count, actualTranslationTested = includeTranslation }), System.Text.Encoding.UTF8);
        }

        private static async Task VerifyProviderFailures()
        {
            await Check("invalid-input-does-not-start-engine", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\verification\missing-engine"))
                {
                    await Throws<ArgumentException>(() => service.TranslateAsync(" \n", "en", CancellationToken.None));
                    await Throws<ArgumentException>(() => service.TranslateAsync(new string('a', 5001), "en", CancellationToken.None));
                    await Throws<ArgumentException>(() => service.TranslateAsync("text", "fr", CancellationToken.None));
                    TranslationService.ValidateInput(new string('a', 5000));
                }
            });
            await Check("missing-offline-engine-visible", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\verification\missing-engine"))
                    await Throws<InvalidOperationException>(() => service.TranslateAsync("Hello", "zh-CN", CancellationToken.None));
            });
            await Check("cancelled-input-does-not-start-engine", async delegate
            {
                using (var service = new TranslationService(@"A:\Agent\Codex\Translation\verification\missing-engine"))
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    await Throws<OperationCanceledException>(() => service.TranslateAsync("Hello", "zh-CN", cancellation.Token));
                }
            });
        }

        private static async Task VerifyGeometry()
        {
            await Check("reverse-drag-and-edge-clipping", delegate
            {
                Rectangle reversed = SelectionForm.NormalizeRegion(new Point(140, 100), new Point(30, 20), new Size(200, 150));
                Assert(reversed == new Rectangle(30, 20, 110, 80), "Reverse drag crop is incorrect.");
                Rectangle clipped = SelectionForm.NormalizeRegion(new Point(40, 30), new Point(300, -80), new Size(200, 150));
                Assert(clipped == new Rectangle(40, 0, 160, 30), "Edge clipping is incorrect.");
                Rectangle outside = SelectionForm.NormalizeRegion(new Point(300, 300), new Point(350, 350), new Size(200, 150));
                Assert(outside.X <= 200 && outside.Y <= 150 && outside.Width == 0 && outside.Height == 0, "Outside selection is not bounded.");
                return Task.FromResult(0);
            });
        }

        private static async Task VerifyOcr()
        {
            string directory = Path.Combine(ArtifactDirectory, "ocr 空格测试");
            var service = new OcrService(directory, @"A:\Agent\Codex\Translation\app\ocr");
            await Check("offline-ocr-english-and-unicode-path", async delegate
            {
                using (var image = TestImage("HELLO WORLD 123"))
                {
                    string text = await service.RecognizeAsync(image, CancellationToken.None);
                    Assert(text.Contains("HELLO") && text.Contains("123"), "English OCR fixture was not recognized.");
                }
                Assert(!Directory.GetFiles(directory).Any(), "OCR left temporary image files.");
            });
            await Check("offline-ocr-chinese", async delegate
            {
                using (var image = TestImage("你好世界"))
                {
                    string text = await service.RecognizeAsync(image, CancellationToken.None);
                    Assert(text.Contains("你好世界"), "Chinese OCR fixture was not recognized.");
                }
            });
            await Check("blank-ocr-is-error-and-cleans-up", async delegate
            {
                using (var image = TestImage(""))
                    await Throws<InvalidOperationException>(() => service.RecognizeAsync(image, CancellationToken.None));
                Assert(!Directory.GetFiles(directory).Any(), "Blank OCR left temporary files.");
            });
            await Check("ocr-cancellation-cleans-up", async delegate
            {
                using (var cancellation = new CancellationTokenSource())
                using (var image = TestImage("HELLO WORLD 123"))
                {
                    cancellation.CancelAfter(20);
                    await Throws<OperationCanceledException>(() => service.RecognizeAsync(image, cancellation.Token));
                }
                Assert(!Directory.GetFiles(directory).Any(), "Cancelled OCR left temporary image files.");
            });
        }

        private static Bitmap TestImage(string text)
        {
            var image = new Bitmap(720, 130);
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Microsoft YaHei UI", 36, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.Clear(Color.White);
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(text, font, Brushes.Black, 24, 36);
            }
            return image;
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static async Task Throws<T>(Func<Task> action) where T : Exception
        {
            try { await action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected exception: " + typeof(T).Name);
        }

        private static async Task Check(string name, Func<Task> action)
        {
            await action();
            passed.Add(name);
            Console.WriteLine("PASS " + name);
        }

    }
}
