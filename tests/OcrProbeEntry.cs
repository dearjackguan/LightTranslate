using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace LightTranslate
{
    internal static class OcrProbeEntry
    {
        private static int Main()
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            File.WriteAllText("worker.pid", Process.GetCurrentProcess().Id.ToString(), Encoding.UTF8);
            string behavior = File.ReadAllText("behavior.txt", Encoding.UTF8).Trim();
            if (behavior == "before-ready") { Thread.Sleep(30000); return 0; }
            if (behavior == "exit") return 17;
            Console.WriteLine("OCR init completed.");
            string request = Console.ReadLine();
            File.WriteAllText("request.read", "ready", Encoding.UTF8);
            if (behavior == "after-ready") { Thread.Sleep(30000); return 0; }
            var values = new JavaScriptSerializer().DeserializeObject(request) as Dictionary<string, object>;
            Console.WriteLine(new JavaScriptSerializer().Serialize(new { code = 100, data = new[] {
                new { text = (string)values["image_path"], score = 1.0, box = new[] { new[] {0,0},
                    new[] {600,0}, new[] {600,20}, new[] {0,20} } } } }));
            Console.Out.Flush();
            Thread.Sleep(30000);
            return 0;
        }
    }
}
