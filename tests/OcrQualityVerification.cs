using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class OcrQualityVerification
    {
        internal static async Task RunAsync()
        {
            string root = @"A:\Agent\Codex\Translation\ocr-evaluation";
            string directory = Path.Combine(root, "service 临时 files");
            var service = new OcrService(directory, @"A:\Agent\Codex\Translation\app\ocr");
            var manifest = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(
                Path.Combine(root, "fixtures", "manifest.json"), Encoding.UTF8)) as Dictionary<string, object>;
            var observations = new List<object>();
            int errors = 0, exact = 0, characters = 0, criticalFailures = 0, reviews = 0;
            foreach (Dictionary<string, object> fixture in (object[])manifest["fixtures"])
            {
                string path = Path.Combine(root, "fixtures", (string)fixture["path"]);
                using (var sha = SHA256.Create())
                    ServiceVerification.Assert(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                        .Replace("-", "").ToLowerInvariant() == (string)fixture["sha256"], "Frozen fixture changed.");
                using (var image = new Bitmap(path))
                {
                    var timer = Stopwatch.StartNew();
                    OcrRecognition result = await service.RecognizeDetailedAsync(image, CancellationToken.None);
                    string expected = Normalize((string)fixture["expected"]), actual = Normalize(result.Text);
                    string[] missing = ((object[])fixture["critical"]).Cast<string>().Where(m => !actual.Contains(m)).ToArray();
                    int edits = Distance(expected, actual);
                    errors += edits; characters += expected.Length;
                    if (edits == 0) exact++;
                    if (missing.Length > 0) criticalFailures++;
                    if (result.NeedsReview) reviews++;
                    observations.Add(new { id = fixture["id"], expected = fixture["expected"], actual = result.Text,
                        errors = edits, missingCritical = missing, needsReview = result.NeedsReview,
                        elapsedSeconds = timer.Elapsed.TotalSeconds });
                }
                if (observations.Count % 12 == 0) Console.WriteLine("Evaluated integrated OCR " + observations.Count + "/72");
            }
            File.WriteAllText(Path.Combine(root, "integrated-ocr-results.json"), new JavaScriptSerializer().Serialize(
                new { count = observations.Count, errors = errors, characters = characters, exact = exact,
                    criticalFailures = criticalFailures, reviews = reviews, cases = observations }), Encoding.UTF8);
            ServiceVerification.Assert(criticalFailures == 0, "Integrated OCR altered a required negation, number or version.");
            ServiceVerification.Assert(exact >= 70 && errors <= 5, "Integrated OCR missed the frozen quality gate.");
            ServiceVerification.Assert(!Directory.GetFiles(directory).Any(), "Integrated OCR left temporary files.");
            ParserCases();
            await ReviewWorkflow();
            Console.WriteLine("PASS integrated OCR " + exact + "/72 exact; " + errors + " character edits; zero critical failures");
        }

        private static void ParserCases()
        {
            foreach (string invalid in new[] { "null", "{}", "[]", "not-json", "{\"code\":\"100\"}",
                "{\"code\":100,\"data\":[]}", "{\"code\":100,\"data\":[{\"text\":\"Hello\",\"score\":2,\"box\":[]}]}",
                "{\"code\":100,\"data\":[{\"text\":\"Hello\",\"score\":1,\"box\":[[0,0],[0,0],[0,0],[0,0]]}]}" })
            {
                try { OcrResponse.Parse(invalid); throw new Exception("Malformed OCR response was accepted."); }
                catch (InvalidDataException) { }
            }
            OcrRecognition review = OcrResponse.Parse("{\"code\":100,\"data\":[{\"text\":\"Do not close.\",\"score\":0.5,\"box\":[[0,0],[200,0],[200,20],[0,20]]}]}");
            ServiceVerification.Assert(review.NeedsReview && review.Text == "Do not close.", "Uncertain OCR lost text or the review marker.");
            Console.WriteLine("PASS OCR response validation and low-score review checks");
        }

        private static string Normalize(string text) { return Regex.Replace(text, "\\s", ""); }

        private static async Task ReviewWorkflow()
        {
            using (var form = new MainForm())
            using (var controller = new AppController(form))
            {
                form.SetResult("Previous completed translation");
                await controller.ApplyRecognitionAsync(new OcrRecognition("不含税 19.99", true), CancellationToken.None);
                ServiceVerification.Assert(form.SourceText == "不含税 19.99" && form.ResultText == "Previous completed translation",
                    "Uncertain OCR changed the previous output or lost its complete source.");
                var status = form.Controls.Find("StatusText", true).Single();
                ServiceVerification.Assert(status.Text.Contains("核对原文"), "Uncertain OCR was automatically translated.");
            }
            Console.WriteLine("PASS uncertain OCR requires source review and preserves previous output");
        }

        private static int Distance(string reference, string actual)
        {
            int[] previous = Enumerable.Range(0, actual.Length + 1).ToArray();
            for (int i = 1; i <= reference.Length; i++)
            {
                var current = new int[actual.Length + 1]; current[0] = i;
                for (int j = 1; j <= actual.Length; j++)
                    current[j] = Math.Min(previous[j] + 1, Math.Min(current[j - 1] + 1,
                        previous[j - 1] + (reference[i - 1] == actual[j - 1] ? 0 : 1)));
                previous = current;
            }
            return previous[actual.Length];
        }
    }
}
