using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class OcrLayoutVerification
    {
        internal static async Task RunAsync()
        {
            var observations = new List<object>();
            var passed = PureLayoutCases();
            string directory = Path.Combine(ServiceVerification.ArtifactDirectory, "layout-temp");
            var service = new OcrService(directory, @"A:\Agent\Codex\Translation\app\ocr");
            using (var image = WrappedText())
            {
                string text = await service.RecognizeAsync(image, CancellationToken.None);
                observations.Add(new { name = "english-soft-wrap", text = text });
                ServiceVerification.Assert(text.Contains("while the update") && !text.Contains("\n"), "Soft wrap still breaks a condition.");
                passed.Add("actual-english-soft-wrap");
                using (var translator = new TranslationService(@"A:\Agent\Codex\Translation\app\engine"))
                    observations.Add(new { name = "english-soft-wrap-translation", text = await translator.TranslateAsync(text, "auto", CancellationToken.None) });
            }
            await ActualLayoutCases(service, observations, passed);
            ServiceVerification.Assert(!Directory.GetFiles(directory).Any(), "OCR layout left temporary files.");
            File.WriteAllText(Path.Combine(ServiceVerification.ArtifactDirectory,
                "ocr-layout-results.json"),
                new JavaScriptSerializer().Serialize(new { passed = passed, observations = observations }), System.Text.Encoding.UTF8);
            Console.WriteLine("PASS OCR layout " + passed.Count + " checks");
        }

        private static List<string> PureLayoutCases()
        {
            var passed = new List<string>();
            Check(passed, "english-wrap", "Do not turn off the computer while the update runs.",
                Line("Do not turn off the computer while", 20, 20, 350), Line("the update runs.", 20, 46, 180));
            Check(passed, "chinese-wrap", "安装尚未完成，请不要关闭电脑。",
                Line("安 装 尚 未 完 成，", 20, 20, 260), Line("请不要关闭电脑。", 20, 46, 200));
            Check(passed, "paragraph-gap", "First paragraph.\nSecond paragraph.",
                Line("First paragraph.", 20, 20, 250), Line("Second paragraph.", 20, 80, 260));
            Check(passed, "list-boundaries", "1. Save your work.\n2. Close the editor.",
                Line("1. Save your work.", 20, 20, 250), Line("2. Close the editor.", 20, 46, 250));
            Check(passed, "list-continuation", "1. Save your work before closing the editor.",
                Line("1. Save your work before", 20, 20, 300), Line("closing the editor.", 38, 46, 220));
            Check(passed, "first-line-indent", "An indented first line continues in the next line.",
                Line("An indented first line", 60, 20, 260), Line("continues in the next line.", 20, 46, 300));
            Check(passed, "hanging-list-indent", "12. Save the document before closing the editor.",
                Line("12. Save the document before", 20, 20, 320), Line("closing the editor.", 60, 46, 280));
            Check(passed, "separate-indented-block", "Main paragraph.\nIndented separate block.",
                Line("Main paragraph.", 20, 20, 250), Line("Indented separate block.", 80, 46, 250));
            Check(passed, "heading-height", "Instructions\nSave the file before closing it.",
                Line("Instructions", 20, 10, 300, 32), Line("Save the file before closing it.", 20, 48, 320));
            Check(passed, "short-line-boundary", "Summary\nSave the document before closing it.",
                Line("Summary", 20, 20, 80), Line("Save the document before closing it.", 20, 46, 300));
            Check(passed, "column-order", "Left first. Left second.\nRight first. Right second.",
                Line("Right second.", 500, 46, 220), Line("Left first.", 20, 20, 220),
                Line("Right first.", 500, 20, 220), Line("Left second.", 20, 46, 220));
            Check(passed, "heading-over-columns", "Heading\nLeft first. Left second.\nRight first. Right second.",
                Line("Heading", 20, 0, 750, 30), Line("Right first.", 500, 48, 220),
                Line("Left first.", 20, 48, 220), Line("Right second.", 500, 74, 220), Line("Left second.", 20, 74, 220));
            Check(passed, "combined-ocr-columns", "Left one. Left two.\nRight one. Right two.",
                Combined("Left one.", "Right one.", 20), Combined("Left two.", "Right two.", 46));
            Check(passed, "identifier-preservation", "Open https://example.com/docs. Email support@example.com.",
                Line("Open https://example.com/docs.", 20, 20, 300), Line("Email support@example.com.", 20, 46, 290));
            var legacy = new Dictionary<string, object> { { "text", "原 文\nSecond line." } };
            ServiceVerification.Assert(OcrLayout.Reconstruct(legacy) == "原文\nSecond line.", "Legacy text was not preserved.");
            var rotated = Result(Line("First", 20, 20, 100), Line("Second", 20, 46, 100));
            rotated["angle"] = 12;
            ServiceVerification.Assert(OcrLayout.Reconstruct(rotated) == "First\nSecond", "Rotated layout was guessed.");
            passed.AddRange(new[] { "legacy-fallback", "rotation-fallback" });
            return passed;
        }

        private static void Check(List<string> passed, string name, string expected, params object[] lines)
        {
            string actual = OcrLayout.Reconstruct(Result(lines));
            ServiceVerification.Assert(actual == expected, name + ": " + actual);
            passed.Add(name);
        }

        private static Dictionary<string, object> Result(params object[] lines)
        {
            return new Dictionary<string, object> { { "text", string.Join("\n", lines.Select(l => (string)((Dictionary<string, object>)l)["text"])) }, { "lines", lines } };
        }

        private static object Line(string text, float x, float y, float width, float height = 20)
        {
            return new Dictionary<string, object> { { "text", text }, { "words", new[] { Word(text, x, y, width, height) } } };
        }

        private static object Word(string text, float x, float y, float width, float height = 20)
        {
            return new Dictionary<string, object> { { "text", text }, { "x", x }, { "y", y }, { "width", width }, { "height", height } };
        }

        private static object Combined(string left, string right, float y)
        {
            return new Dictionary<string, object> { { "text", left + " " + right },
                { "words", new[] { Word(left, 20, y, 200), Word(right, 500, y, 200) } } };
        }

        private static async Task ActualLayoutCases(OcrService service, List<object> observations, List<string> passed)
        {
            using (var image = ChineseText())
            {
                string text = await service.RecognizeAsync(image, CancellationToken.None);
                ServiceVerification.Assert(!text.Contains("\n") && text.Contains("尚未完成") && text.Contains("关闭电脑"), "Actual Chinese wrap failed: " + text);
                observations.Add(new { name = "chinese-soft-wrap", text = text });
                passed.Add("actual-chinese-soft-wrap");
            }
            using (var image = Columns())
            {
                image.Save(Path.Combine(ServiceVerification.ArtifactDirectory, "ocr-columns.png"));
                string text = await service.RecognizeAsync(image, CancellationToken.None);
                ServiceVerification.Assert(text.Contains("before closing") && text.Contains("while the update"), "Columns were not independently joined: " + text);
                ServiceVerification.Assert(text.IndexOf("closing", StringComparison.Ordinal) < text.IndexOf("Keep", StringComparison.Ordinal), "Column reading order changed: " + text);
                observations.Add(new { name = "two-column-reading-order", text = text });
                passed.Add("actual-two-columns");
            }
        }

        private static Bitmap ChineseText()
        {
            var image = new Bitmap(850, 160);
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Microsoft YaHei UI", 30, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.Clear(Color.White);
                graphics.DrawString("安装尚未完成，", font, Brushes.Black, 24, 24);
                graphics.DrawString("请不要关闭电脑。", font, Brushes.Black, 24, 60);
            }
            return image;
        }

        private static Bitmap Columns()
        {
            var image = new Bitmap(1250, 180);
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Microsoft YaHei UI", 28, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.Clear(Color.White);
                graphics.DrawString("Save the document before", font, Brushes.Black, 24, 24);
                graphics.DrawString("closing the editor.", font, Brushes.Black, 24, 60);
                graphics.DrawString("Keep the computer on while", font, Brushes.Black, 670, 24);
                graphics.DrawString("the update is running.", font, Brushes.Black, 670, 60);
            }
            return image;
        }

        private static Bitmap WrappedText()
        {
            var image = new Bitmap(850, 160);
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Microsoft YaHei UI", 30, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.Clear(Color.White);
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString("Do not turn off the computer while", font, Brushes.Black, 24, 24);
                graphics.DrawString("the update is being installed.", font, Brushes.Black, 24, 60);
            }
            return image;
        }
    }
}
