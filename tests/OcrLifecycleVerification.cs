using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LightTranslate
{
    internal static class OcrLifecycleVerification
    {
        private const string Probe = @"A:\Agent\Codex\Translation\ocr-evaluation\process-probe";
        internal static async Task RunAsync()
        {
            string executable = Path.Combine(Probe, "OcrProbe.exe");
            string path = Path.Combine(Probe, "中文 folder", "猫🐈.png");
            WriteBehavior("echo");
            var result = OcrResponse.Parse(await OcrProcess.RunAsync(executable, path, 2048, CancellationToken.None));
            ServiceVerification.Assert(result.Text == path, "OCR request Unicode/space path changed.");
            AssertProbeStopped();
            Console.WriteLine("PASS OCR ASCII request and Unicode response roundtrip plus worker cleanup");
            foreach (string behavior in new[] { "before-ready", "after-ready" })
            {
                WriteBehavior(behavior);
                using (var cancellation = new CancellationTokenSource())
                {
                    Task<string> operation = OcrProcess.RunAsync(executable, path, 2048, cancellation.Token);
                    await WaitUntilFile(behavior == "after-ready" ? "request.read" : "worker.pid");
                    cancellation.Cancel();
                    await ServiceVerification.Throws<OperationCanceledException>(() => operation);
                    AssertProbeStopped();
                }
                Console.WriteLine("PASS OCR cancellation " + behavior + " and worker cleanup");
            }
            WriteBehavior("exit");
            await ServiceVerification.Throws<InvalidOperationException>(() => OcrProcess.RunAsync(executable, path, 2048, CancellationToken.None));
            AssertProbeStopped();
            WriteBehavior("before-ready");
            await ServiceVerification.Throws<TimeoutException>(() => OcrProcess.RunAsync(executable, path, 2048, CancellationToken.None));
            AssertProbeStopped();
            Console.WriteLine("PASS OCR abnormal exit and actual deadline cleanup");
            await ActualWorkerCases();
        }

        private static async Task ActualWorkerCases()
        {
            string directory = Path.Combine(Probe, "actual-temp");
            string engine = @"A:\Agent\Codex\Translation\app\ocr";
            using (var image = new Bitmap(700, 100))
            {
                using (var drawing = Graphics.FromImage(image))
                using (var font = new Font("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Pixel))
                { drawing.Clear(Color.White); drawing.DrawString("Do not close 123", font, Brushes.Black, 16, 16); }
                await ServiceVerification.Throws<InvalidOperationException>(() => new OcrService(directory,
                    Path.Combine(Probe, "missing-engine")).RecognizeAsync(image, CancellationToken.None));
                var service = new OcrService(directory, engine);
                using (var cancellation = new CancellationTokenSource())
                {
                    Task<string> operation = service.RecognizeAsync(image, cancellation.Token);
                    var worker = Process.GetProcessesByName("RapidOCR-json").FirstOrDefault(p =>
                        string.Equals(p.MainModule.FileName, Path.Combine(engine, "RapidOCR-json.exe"), StringComparison.OrdinalIgnoreCase));
                    ServiceVerification.Assert(worker != null, "Actual OCR worker did not start.");
                    int pid = worker.Id; worker.Dispose();
                    cancellation.Cancel();
                    await ServiceVerification.Throws<OperationCanceledException>(() => operation);
                    ServiceVerification.Assert(!IsRunning(pid), "Actual cancelled OCR worker survived.");
                }
                string text = await service.RecognizeAsync(image, CancellationToken.None);
                ServiceVerification.Assert(text.Contains("not close 123"), "OCR did not recover after cancellation.");
            }
            ServiceVerification.Assert(!Directory.GetFiles(directory).Any(), "OCR lifecycle left temporary image files.");
            Console.WriteLine("PASS actual OCR cancellation/recovery, missing component and image cleanup");
        }

        private static void WriteBehavior(string behavior)
        {
            string pid = Path.Combine(Probe, "worker.pid");
            if (File.Exists(pid)) File.Delete(pid);
            string read = Path.Combine(Probe, "request.read");
            if (File.Exists(read)) File.Delete(read);
            File.WriteAllText(Path.Combine(Probe, "behavior.txt"), behavior, Encoding.UTF8);
        }
        private static async Task WaitUntilFile(string name)
        {
            for (int i = 0; i < 100 && !File.Exists(Path.Combine(Probe, name)); i++) await Task.Delay(10);
            ServiceVerification.Assert(File.Exists(Path.Combine(Probe, name)), "OCR probe did not reach the required phase.");
        }
        private static void AssertProbeStopped()
        {
            int pid = int.Parse(File.ReadAllText(Path.Combine(Probe, "worker.pid"), Encoding.UTF8));
            ServiceVerification.Assert(!IsRunning(pid), "OCR probe survived its operation.");
        }
        private static bool IsRunning(int pid)
        {
            try { using (var process = Process.GetProcessById(pid)) return !process.HasExited; }
            catch (ArgumentException) { return false; }
        }
    }
}
