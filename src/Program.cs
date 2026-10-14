using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;

[assembly: AssemblyTitle("LightTranslate")]
[assembly: AssemblyDescription("Minimal screenshot and input translator")]
[assembly: AssemblyCompany("JackGuan")]
[assembly: AssemblyCopyright("Copyright (c) 2026 JackGuan and LightTranslate contributors")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace LightTranslate
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new MainForm())
            using (var controller = new AppController(form))
                Application.Run(form);
        }
    }
}
