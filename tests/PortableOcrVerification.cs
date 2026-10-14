using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class PortableOcrVerification
    {
        internal static async Task RunAsync(string application)
        {
            const string fixtures = @"A:\Agent\Codex\Translation\ocr-evaluation\fixtures";
            string temporary = Path.Combine(application, "temp");
            var serializer = new JavaScriptSerializer();
            var manifest = serializer.DeserializeObject(File.ReadAllText(Path.Combine(fixtures, "manifest.json"), Encoding.UTF8))
                as Dictionary<string, object>;
            var records = ((object[])manifest["fixtures"]).Cast<Dictionary<string, object>>();
            var observations = new List<object>();
            var service = new OcrService(temporary, Path.Combine(application, "ocr"));
            using (var translator = new TranslationService(Path.Combine(application, "engine")))
            {
                foreach (string name in new[] { "zh-tax-12-dark.png", "en-update-12-dark.png", "zh-update-12-light.png" })
                using (var image = new Bitmap(Path.Combine(fixtures, name)))
                {
                    var reference = records.Single(item => (string)item["id"] == name);
                    OcrRecognition result = await service.RecognizeDetailedAsync(image, CancellationToken.None);
                    ServiceVerification.Assert(Regex.Replace(result.Text, "\\s", "")
                        == Regex.Replace((string)reference["expected"], "\\s", "") && !result.NeedsReview,
                        "Relocated OCR changed a complete fixture.");
                    TranslationResult translated = await translator.TranslateWithDirectionAsync(result.Text, "auto", CancellationToken.None);
                    ServiceVerification.Assert(translated.Source == (string)reference["language"]
                        && !string.IsNullOrWhiteSpace(translated.Text), "Relocated OCR/automatic translation failed.");
                    observations.Add(new { fixture = name, original = result.Text, translated = translated.Text, source = translated.Source });
                }
            }
            ServiceVerification.Assert(!Directory.GetFiles(temporary).Any(), "Relocated OCR left temporary files.");
            File.WriteAllText(@"A:\Agent\Codex\Translation\release\portable-ocr-results.json",
                serializer.Serialize(new { application = application, cases = observations, cleanupPassed = true }), Encoding.UTF8);
            Console.WriteLine("PASS relocated actual OCR adapter and automatic translation with Unicode/space model and temporary paths");
        }
    }
}
