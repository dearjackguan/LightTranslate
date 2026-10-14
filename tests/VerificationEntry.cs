using System;
using System.Linq;
using System.Windows.Forms;

namespace LightTranslate
{
    internal static class VerificationEntry
    {
        [STAThread]
        private static int Main(string[] arguments)
        {
            try
            {
                if (arguments.Length == 1 && arguments[0] == "refinement-interactions")
                    RefinementInteractionVerification.Run();
                else if (arguments.Length == 1 && arguments[0] == "ocr-development")
                    OcrRefinementVerification.RunAsync("development").GetAwaiter().GetResult();
                else if (arguments.Length == 1 && arguments[0] == "ocr-acceptance")
                    OcrRefinementVerification.RunAsync("acceptance").GetAwaiter().GetResult();
                else if (arguments.Length == 2 && arguments[0] == "portable-ocr")
                    PortableOcrVerification.RunAsync(arguments[1]).GetAwaiter().GetResult();
                else if (arguments.Length == 1 && (arguments[0] == "services" || arguments[0] == "local"))
                    ServiceVerification.RunAsync(arguments[0] == "services").GetAwaiter().GetResult();
                else if (arguments.Length == 1 && arguments[0] == "gui")
                    return GuiVerification.Run();
                else if (arguments.Length == 1 && arguments[0] == "gui-messages")
                    return GuiVerification.Run(true);
                else if (arguments.Length == 1 && arguments[0] == "audit")
                    return AuditVerification.Run();
                else if (arguments.Length == 1 && arguments[0] == "layout")
                    OcrLayoutVerification.RunAsync().GetAwaiter().GetResult();
                else if (arguments.Length == 1 && arguments[0] == "small-text")
                    SmallTextVerification.RunAsync().GetAwaiter().GetResult();
                else if (arguments.Length == 1 && arguments[0] == "ocr-quality")
                    OcrQualityVerification.RunAsync().GetAwaiter().GetResult();
                else if (arguments.Length == 1 && arguments[0] == "ocr-lifecycle")
                    OcrLifecycleVerification.RunAsync().GetAwaiter().GetResult();
                else
                    throw new ArgumentException("Use local, services, gui, gui-messages, audit, layout, small-text, ocr-quality, ocr-lifecycle or portable-ocr <application>.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message);
                Console.Error.WriteLine(string.Join("\n", (error.StackTrace ?? "").Split('\n').Take(5)));
                return 1;
            }
        }
    }
}
