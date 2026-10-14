using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LightTranslate
{
    internal sealed class OcrService
    {
        private readonly string temporaryDirectory;
        private readonly string engineDirectory;
        internal static string DefaultEngineDirectory
        { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr"); } }

        internal OcrService(string temporaryDirectory) : this(temporaryDirectory, DefaultEngineDirectory) { }
        internal OcrService(string temporaryDirectory, string engineDirectory)
        {
            this.temporaryDirectory = temporaryDirectory;
            this.engineDirectory = engineDirectory;
        }

        internal async Task<string> RecognizeAsync(Bitmap image, CancellationToken token)
        { return (await RecognizeDetailedAsync(image, token).ConfigureAwait(false)).Text; }

        internal async Task<OcrRecognition> RecognizeDetailedAsync(Bitmap image, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 16000000)
                throw new InvalidOperationException("截图超过文字识别的尺寸限制，请缩小框选区域。");
            string executable = Path.Combine(engineDirectory, "RapidOCR-json.exe");
            ValidateDeployment(executable);
            Directory.CreateDirectory(temporaryDirectory);
            string path = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".png");
            try
            {
                OcrImagePreparation.Save(image, path);
                int maximumSide = Math.Min(4096, Math.Max(2048, Math.Max(image.Width, image.Height)));
                string response = await OcrProcess.RunAsync(executable, path, maximumSide, token).ConfigureAwait(false);
                return OcrResponse.Parse(response);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private void ValidateDeployment(string executable)
        {
            if (!File.Exists(executable)) throw MissingEngine();
            foreach (string name in new[] { "ch_PP-OCRv4_det_infer.onnx", "ch_ppocr_mobile_v2.0_cls_infer.onnx",
                "rec_ch_PP-OCRv4_infer.onnx", "dict_chinese.txt" })
                if (!File.Exists(Path.Combine(engineDirectory, "models", name))) throw MissingEngine();
        }

        private static InvalidOperationException MissingEngine()
        { return new InvalidOperationException("离线文字识别组件缺失，请重新解压完整离线包。"); }
    }
}
