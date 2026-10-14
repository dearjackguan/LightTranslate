using System;
using System.Collections.Generic;
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
    internal static class OcrRefinementVerification
    {
        internal static async Task RunAsync(string split)
        {
            const string root = @"A:\Agent\Codex\Translation\refinement";
            string fixtures = Path.Combine(root, "ocr-fixtures");
            var serializer = new JavaScriptSerializer();
            var manifest = serializer.DeserializeObject(File.ReadAllText(Path.Combine(fixtures, "manifest.json"), Encoding.UTF8))
                as Dictionary<string, object>;
            var service = new OcrService(Path.Combine(root, "ocr-temp"), @"A:\Agent\Codex\Translation\app\ocr");
            var results = new List<object>();
            int exact = 0, missing = 0, review = 0;
            foreach (var item in ((object[])manifest["fixtures"]).Cast<Dictionary<string, object>>().Where(i => (string)i["split"] == split))
            {
                string path = Path.Combine(fixtures, (string)item["path"]);
                using (var algorithm = SHA256.Create())
                    ServiceVerification.Assert(BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path)))
                        .Replace("-", "").ToLowerInvariant() == (string)item["sha256"], "Refinement fixture changed.");
                using (var image = new Bitmap(path))
                {
                    var result = await service.RecognizeDetailedAsync(image, CancellationToken.None);
                    string actual = Regex.Replace(result.Text, "\\s", "");
                    var lost = ((object[])item["critical"]).Cast<string>().Where(s => !actual.Contains(s)).ToArray();
                    bool match = actual == Regex.Replace((string)item["expected"], "\\s", "");
                    if (match) exact++; if (lost.Length > 0) missing++; if (result.NeedsReview) review++;
                    results.Add(new { id = item["id"], expected = item["expected"], actual = result.Text,
                        exact = match, missingCritical = lost, needsReview = result.NeedsReview });
                }
            }
            File.WriteAllText(Path.Combine(root, "ocr-" + split + "-results.json"), serializer.Serialize(
                new { count = results.Count, exact = exact, missingCritical = missing, reviews = review, cases = results }), Encoding.UTF8);
            Console.WriteLine("OCR refinement " + split + ": " + exact + "/" + results.Count + " exact; critical failures=" + missing + "; review=" + review);
            ServiceVerification.Assert(!Directory.GetFiles(Path.Combine(root, "ocr-temp")).Any(), "Refinement OCR left images.");
            if (split == "acceptance")
                ServiceVerification.Assert(results.Count == 24 && exact == 24 && missing == 0,
                    "Frozen OCR acceptance failed; inspect the complete outputs.");
        }
    }
}
