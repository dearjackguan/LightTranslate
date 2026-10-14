using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class OcrResponse
    {
        internal static OcrRecognition Parse(string json)
        {
            Dictionary<string, object> response;
            try { response = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>; }
            catch (ArgumentException) { throw InvalidResponse(); }
            object raw;
            if (response == null || !response.TryGetValue("code", out raw) || !(raw is int)) throw InvalidResponse();
            if ((int)raw == 101) throw new InvalidOperationException("未识别到文字，请框选清晰的文字区域。");
            if ((int)raw != 100) throw new InvalidOperationException("离线文字识别失败，请重新截图。");
            object[] items = response.ContainsKey("data") ? response["data"] as object[] : null;
            if (items == null || items.Length == 0) throw InvalidResponse();
            var lines = new List<object>();
            var texts = new List<string>();
            bool review = false;
            double angle = 0;
            foreach (object item in items)
            {
                var block = item as Dictionary<string, object>;
                string text = block != null && block.ContainsKey("text") ? block["text"] as string : null;
                double score;
                if (string.IsNullOrWhiteSpace(text) || !Number(block, "score", out score) || score < 0 || score > 1)
                    throw InvalidResponse();
                object[] corners = block.ContainsKey("box") ? block["box"] as object[] : null;
                var rectangle = ReadRectangle(corners, ref angle);
                rectangle["text"] = text;
                lines.Add(new Dictionary<string, object> { { "text", text }, { "words", new[] { rectangle } } });
                texts.Add(text);
                review |= score < 0.90;
            }
            string joined = OcrLayout.Reconstruct(new Dictionary<string, object> {
                { "text", string.Join("\n", texts) }, { "lines", lines.ToArray() }, { "angle", angle } });
            return new OcrRecognition(joined, review);
        }
        private static Dictionary<string, object> ReadRectangle(object[] corners, ref double angle)
        {
            if (corners == null || corners.Length != 4) throw InvalidResponse();
            var x = new double[4]; var y = new double[4];
            for (int i = 0; i < 4; i++)
            {
                object[] point = corners[i] as object[];
                if (point == null || point.Length != 2 || !Numeric(point[0], out x[i]) || !Numeric(point[1], out y[i]))
                    throw InvalidResponse();
            }
            double width = x.Max() - x.Min(), height = y.Max() - y.Min();
            if (width <= 0 || height <= 0) throw InvalidResponse();
            double slope = Math.Atan2(y[1] - y[0], x[1] - x[0]) * 180 / Math.PI;
            if (Math.Abs(slope) > Math.Abs(angle)) angle = slope;
            return new Dictionary<string, object> { { "x", x.Min() }, { "y", y.Min() },
                { "width", width }, { "height", height } };
        }
        private static bool Number(Dictionary<string, object> value, string key, out double number)
        {
            number = 0;
            object raw;
            return value != null && value.TryGetValue(key, out raw) && Numeric(raw, out number);
        }
        private static bool Numeric(object value, out double number)
        {
            number = 0;
            if (!(value is int || value is long || value is decimal || value is double)) return false;
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return !double.IsNaN(number) && !double.IsInfinity(number);
        }
        private static InvalidDataException InvalidResponse()
        { return new InvalidDataException("离线文字识别返回了无效结果，请重新截图。"); }
    }
}
