using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class SmallTextVerification
    {
        internal static async Task RunAsync()
        {
            string root = ServiceVerification.ArtifactDirectory;
            string directory = Path.Combine(root, "small-text-temp");
            var service = new OcrService(directory, @"A:\Agent\Codex\Translation\app\ocr");
            var assembly = Assembly.LoadFile(Path.Combine(root, "LightTranslate-pre-small-text-1.0.3.exe"));
            Type legacy = assembly.GetType("LightTranslate.OcrService", true);
            var constructor = legacy.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            object previous = constructor.Invoke(new object[] { directory });
            MethodInfo recognize = legacy.GetMethod("RecognizeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var observations = new List<object>();
            int baselineErrors = 0, currentErrors = 0, characters = 0, exact = 0;
            foreach (bool chinese in new[] { false, true })
            foreach (int pixels in new[] { 10, 12, 14, 16 })
            {
                string expected = chinese ? "更新尚未完成，请不要关闭电脑。" : "Save your work before closing the window.";
                using (var image = Fixture(expected, chinese, pixels))
                {
                    string before = await ObserveAsync(() => (Task<string>)recognize.Invoke(previous, new object[] { image, CancellationToken.None }));
                    string after = await ObserveAsync(() => service.RecognizeAsync(image, CancellationToken.None));
                    int oldErrors = Distance(Normalize(expected), Normalize(before));
                    int newErrors = Distance(Normalize(expected), Normalize(after));
                    baselineErrors += oldErrors; currentErrors += newErrors; characters += Normalize(expected).Length;
                    if (newErrors == 0) exact++;
                    observations.Add(new { chinese = chinese, pixels = pixels, expected = expected,
                        before = before, after = after, beforeErrors = oldErrors, afterErrors = newErrors });
                    if (!chinese && pixels >= 12)
                        ServiceVerification.Assert(after == expected, "Small English recognition regressed: " + after);
                    if (chinese && pixels == 16)
                        ServiceVerification.Assert(after.Contains("请不要关闭电脑"), "Small Chinese lost the prohibition: " + after);
                }
            }
            bool improved = currentErrors < baselineErrors;
            File.WriteAllText(Path.Combine(root, "small-text-results.json"), new JavaScriptSerializer().Serialize(
                new { improved = improved, baselineErrors = baselineErrors, currentErrors = currentErrors,
                    characters = characters, exact = exact, cases = observations,
                    scope = "Authored ClearType fixtures at 10/12/14/16 physical pixels; not general OCR accuracy." }), Encoding.UTF8);
            ServiceVerification.Assert(improved, "Small-text aggregate character errors did not improve.");
            ServiceVerification.Assert(!Directory.GetFiles(directory).Any(), "Small-text OCR left temporary files.");
            Console.WriteLine("PASS small text 8 real OCR comparisons; errors " + baselineErrors + " -> " + currentErrors);
        }

        private static async Task<string> ObserveAsync(Func<Task<string>> action)
        {
            try { return await action(); }
            catch (InvalidOperationException error)
            {
                if (error.Message.StartsWith("未识别到文字", StringComparison.Ordinal)) return "";
                throw;
            }
        }

        private static Bitmap Fixture(string text, bool chinese, int pixels)
        {
            var image = new Bitmap(540, 65);
            using (var drawing = Graphics.FromImage(image))
            using (var font = new Font(chinese ? "Microsoft YaHei UI" : "Segoe UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                drawing.Clear(Color.White);
                drawing.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                drawing.DrawString(text, font, Brushes.Black, 10, 10);
            }
            return image;
        }

        private static string Normalize(string text)
        { return Regex.Replace(text, "\\s", ""); }

        private static int Distance(string expected, string actual)
        {
            int[] previous = Enumerable.Range(0, actual.Length + 1).ToArray();
            for (int i = 1; i <= expected.Length; i++)
            {
                var current = new int[actual.Length + 1]; current[0] = i;
                for (int j = 1; j <= actual.Length; j++)
                    current[j] = Math.Min(previous[j] + 1, Math.Min(current[j - 1] + 1,
                        previous[j - 1] + (expected[i - 1] == actual[j - 1] ? 0 : 1)));
                previous = current;
            }
            return previous[actual.Length];
        }
    }
}
