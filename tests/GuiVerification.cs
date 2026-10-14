using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LightTranslate
{
    internal static class GuiVerification
    {
        private static readonly List<string> passed = new List<string>();
        private static readonly List<string> unavailable = new List<string>();
        private static Process application;
        private static IntPtr window;
        private static IntPtr source;
        private static IntPtr result;
        private static IntPtr status;
        private static FixtureForm fixture;
        private static bool inputTranslated;
        private static bool screenshotTranslated;
        private static long idleBytes;
        private static long peakBytes;
        private static int exitCode;
        private static bool useWindowMessages;

        internal static int Run(bool windowMessages = false)
        {
            useWindowMessages = windowMessages;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            fixture = new FixtureForm();
            fixture.Shown += async delegate
            {
                Point cursor = Cursor.Position;
                try { await ExerciseAsync(); }
                catch (Exception error)
                {
                    Console.Error.WriteLine("FAIL GUI " + error.GetType().Name + ": " + error.Message);
                    exitCode = 1;
                }
                finally
                {
                    IntPtr overlay = FindWindow(null, "轻译 · 框选截图");
                    if (overlay != IntPtr.Zero) PostMessage(overlay, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    Cursor.Position = cursor;
                    fixture.Close();
                }
            };
            Application.Run(fixture);
            fixture.Dispose();
            bool accepted = exitCode == 0 && inputTranslated && screenshotTranslated;
            WriteReport(accepted);
            if (!accepted) exitCode = 1;
            if (application != null) application.Dispose();
            return exitCode;
        }

        private static async Task ExerciseAsync()
        {
            application = Process.GetProcessesByName("LightTranslate").FirstOrDefault(p =>
                p.MainModule.FileName == @"A:\Agent\Codex\Translation\app\LightTranslate.exe");
            if (application == null)
                application = Process.Start(@"A:\Agent\Codex\Translation\app\LightTranslate.exe");
            await WaitUntil(delegate { application.Refresh(); return application.MainWindowHandle != IntPtr.Zero; });
            window = application.MainWindowHandle;
            Console.WriteLine("GUI initial window=" + Bounds(window) + " iconic=" + IsIconic(window));
            if (IsIconic(window))
            {
                ShowWindow(window, 9);
                await WaitUntil(() => !IsIconic(window) && Bounds(window).Height >= 400);
            }
            var edits = Children(window).Where(h => ClassName(h).Contains("EDIT"))
                .OrderBy(h => Bounds(h).Top).ToArray();
            ServiceVerification.Assert(edits.Length == 2, "Expected original and translation editors.");
            source = edits[0];
            result = edits[1];
            status = Children(window).Where(h => ClassName(h).Contains("STATIC"))
                .OrderByDescending(h => Bounds(h).Top).Skip(1).First();
            Console.WriteLine("GUI initial status=" + ReadText(status));
            await Task.Delay(5000);
            application.Refresh();
            idleBytes = application.WorkingSet64;
            ServiceVerification.Assert(idleBytes <= 80L * 1024 * 1024, "Idle memory exceeds the approved target.");
            await ExerciseInputAsync();
            await ExerciseScreenshotsAsync();
            await ExerciseWrappedScreenshotAsync();
            await ExerciseSmallScreenshotAsync();
            await ExerciseSmallChineseScreenshotAsync();
            await ExerciseBlankScreenshotAsync();
            await ExerciseFinalTranslationAsync();
            Console.WriteLine("GUI final window=" + Bounds(window) + " iconic=" + IsIconic(window));
            SaveWindowImage(Path.Combine(ServiceVerification.ArtifactDirectory, "main-window.png"));
            application.Refresh();
            peakBytes = application.PeakWorkingSet64;
            await ExerciseCloseDuringTranslationAsync();
            Rectangle desktop = SystemInformation.VirtualScreen;
        }

        private static void WriteReport(bool accepted)
        {
            if (application != null) application.Refresh();
            Rectangle desktop = SystemInformation.VirtualScreen;
            var report = new { passed = passed, unavailable = unavailable, count = passed.Count, accepted = accepted,
                inputTranslated = inputTranslated,
                screenshotTranslated = screenshotTranslated, idleWorkingSetBytes = idleBytes,
                peakWorkingSetBytes = peakBytes,
                dpi = window == IntPtr.Zero ? 0 : GetDpiForWindow(window),
                utc = DateTime.UtcNow.ToString("o"),
                inputMethod = useWindowMessages ? "Window messages; physical keyboard/mouse injection is not verified" : "Physical input injection",
                desktop = new { x = desktop.X, y = desktop.Y, width = desktop.Width, height = desktop.Height } };
            File.WriteAllText(Path.Combine(ServiceVerification.ArtifactDirectory,
                useWindowMessages ? "gui-message-results.json" : "gui-results.json"),
                new JavaScriptSerializer().Serialize(report), Encoding.UTF8);
        }

        private static async Task ExerciseInputAsync()
        {
            SetText(source, "");
            Click("翻译  Ctrl+Enter");
            await WaitUntil(() => ReadText(status).StartsWith("请输入"));
            Pass("gui-empty-input-feedback");
            SetText(source, new string('x', 5001));
            ServiceVerification.Assert(ReadText(source).Length == 5001, "Editor silently truncated input.");
            Click("翻译  Ctrl+Enter");
            await WaitUntil(() => ReadText(status).Contains("5000"));
            ServiceVerification.Assert(ReadText(source).Length == 5001, "Validation altered the original.");
            Pass("gui-overlimit-not-truncated");
            SetText(source, "Hello world");
            bool installed = File.Exists(@"A:\Agent\Codex\Translation\app\engine\python\python.exe");
            if (installed)
            {
                string previousResult = ReadText(result);
                Click("翻译  Ctrl+Enter");
                Click("取消");
                await WaitUntil(() => ReadText(status) == "已取消");
                ServiceVerification.Assert(ReadText(result) == previousResult, "Cancellation changed the previous translation.");
                Pass("gui-input-cancellation");
            }
            else unavailable.Add("gui-active-inference-cancellation: runtime is absent");
            Click("翻译  Ctrl+Enter");
            await WaitUntil(TranslationFinished);
            inputTranslated = ReadText(status).StartsWith("翻译完成");
            if (inputTranslated)
            {
                ServiceVerification.Assert(ReadText(result).Contains("世界"), "Unexpected input translation.");
                ServiceVerification.Assert(ReadText(status).Contains("英译中"), "Automatic English direction is not shown.");
                Pass("gui-live-input-translation");
            }
            else
            {
                Pass("gui-offline-engine-error-visible-and-original-preserved");
                unavailable.Add("gui-live-input-translation: offline inference did not complete");
            }
            ServiceVerification.Assert(ReadText(source) == "Hello world", "Original was lost after the request.");
        }

        private static async Task ExerciseScreenshotsAsync()
        {
            await WaitUntil(() => IsWindowEnabled(FindButton("截图翻译  Ctrl+Alt+S")));
            bool observed = false;
            using (var driver = SelectionDriver(true, false, () => observed = true))
            {
                if (useWindowMessages)
                    PostMessage(window, NativeMethods.HotkeyMessage, new IntPtr(NativeMethods.HotkeyId), IntPtr.Zero);
                else
                {
                    PressKey((byte)Keys.ControlKey, false);
                    PressKey((byte)Keys.Menu, false);
                    PressKey((byte)Keys.S, false);
                    await Task.Delay(50);
                    PressKey((byte)Keys.S, true);
                    PressKey((byte)Keys.Menu, true);
                    PressKey((byte)Keys.ControlKey, true);
                }
                await WaitUntil(() => observed && TranslationFinished());
            }
            ServiceVerification.Assert(ReadText(source).Contains("HELLO") && ReadText(source).Contains("123"),
                "Actual screen region was not recognized.");
            screenshotTranslated = ReadText(status).StartsWith("翻译完成");
            Pass(useWindowMessages ? "gui-hotkey-message-screen-capture-and-ocr" : "gui-global-shortcut-screen-capture-and-ocr");
            if (screenshotTranslated)
            {
                ServiceVerification.Assert(ReadText(result).Contains("世界"), "Unexpected screenshot translation.");
                Pass("gui-live-screenshot-translation");
            }
            else unavailable.Add("gui-live-screenshot-translation: offline inference did not complete");
            string before = ReadText(source);
            string previousResult = ReadText(result);
            observed = false;
            using (var driver = SelectionDriver(false, false, () => observed = true))
            {
                Click("截图翻译  Ctrl+Alt+S");
                await WaitUntil(() => observed && ReadText(status) == "已取消截图" && IsWindowEnabled(FindButton("截图翻译  Ctrl+Alt+S")));
            }
            ServiceVerification.Assert(ReadText(source) == before && ReadText(result) == previousResult,
                "Cancelled screenshot replaced existing text.");
            Pass("gui-escape-cancels-and-preserves-text");
            observed = false;
            using (var driver = SelectionDriver(false, true, () => observed = true))
            {
                Click("截图翻译  Ctrl+Alt+S");
                await WaitUntil(() => observed && ReadText(status) == "已取消截图" && IsWindowEnabled(FindButton("截图翻译  Ctrl+Alt+S")));
            }
            Pass("gui-right-click-cancels");
        }

        private static async Task ExerciseBlankScreenshotAsync()
        {
            fixture.SampleText = "";
            fixture.Invalidate();
            await Task.Delay(100);
            bool observed = false;
            using (var driver = SelectionDriver(true, false, () => observed = true))
            {
                Click("截图翻译  Ctrl+Alt+S");
                await WaitUntil(() => observed && ReadText(status).StartsWith("未识别"));
            }
            string temporary = @"A:\Agent\Codex\Translation\app\temp";
            ServiceVerification.Assert(!Directory.Exists(temporary) || Directory.GetFiles(temporary).Length == 0,
                "GUI operation left image or worker script files.");
            Pass("gui-blank-ocr-feedback-and-cleanup");
        }

        private static async Task ExerciseWrappedScreenshotAsync()
        {
            fixture.SampleText = "Do not turn off the computer while\nthe update is being installed.";
            fixture.SampleFontSize = 30;
            fixture.Invalidate();
            await Task.Delay(100);
            bool observed = false;
            using (var driver = SelectionDriver(true, false, () => observed = true))
            {
                Click("截图翻译  Ctrl+Alt+S");
                await WaitUntil(() => observed && TranslationFinished());
            }
            ServiceVerification.Assert(ReadText(source).Contains("while the update") && !ReadText(source).Contains("\n"),
                "Captured soft wraps still break a condition.");
            ServiceVerification.Assert(ReadText(result).Contains("不") && ReadText(result).Contains("更新"),
                "The captured update prohibition is missing.");
            Pass("gui-wrapped-screenshot-context");
        }

        private static async Task ExerciseFinalTranslationAsync()
        {
            await ExerciseChineseDirectionsAsync();
            IntPtr direction = Children(window).First(h => ClassName(h).Contains("COMBOBOX"));
            SendMessage(direction, 0x014E, IntPtr.Zero, IntPtr.Zero);
            SetText(source, "HELLO WORLD 123");
            Click("翻译  Ctrl+Enter");
            await WaitUntil(TranslationFinished);
            ServiceVerification.Assert(ReadText(result).Contains("世界") && ReadText(result).Contains("123"),
                "Final screenshot fixture translation did not preserve its meaning and number.");
            IDataObject originalClipboard = Clipboard.GetDataObject();
            try
            {
                Click("复制译文");
                ServiceVerification.Assert(Clipboard.GetText() == ReadText(result), "Copying the actual translation failed.");
                Pass("gui-copy-real-translation");
            }
            finally
            {
                if (originalClipboard == null) Clipboard.Clear();
                else Clipboard.SetDataObject(originalClipboard, true);
            }
        }

        private static async Task ExerciseSmallScreenshotAsync()
        {
            fixture.SampleText = "Save your work before closing the window.";
            fixture.SampleFontSize = 12;
            fixture.SampleFontName = "Segoe UI";
            fixture.SampleRendering = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            fixture.Invalidate();
            await Task.Delay(100);
            bool observed = false;
            using (var driver = SelectionDriver(true, false, () => observed = true))
            {
                Click("截图翻译  Ctrl+Alt+S");
                await WaitUntil(() => observed && TranslationFinished());
            }
            ServiceVerification.Assert(ReadText(source) == fixture.SampleText,
                "The actual 12-pixel screenshot was not recognized exactly: " + ReadText(source));
            ServiceVerification.Assert(ReadText(status).StartsWith("翻译完成") && ReadText(result).Contains("窗口"),
                "The actual small screenshot did not complete offline translation.");
            Pass("gui-small-text-screenshot-and-translation");
        }

        private static async Task ExerciseSmallChineseScreenshotAsync()
        {
            fixture.SampleText = "更新安装期间，请勿关闭计算机。";
            fixture.SampleFontSize = 12;
            fixture.SampleFontName = "Microsoft YaHei UI";
            foreach (Color background in new[] { Color.White, Color.FromArgb(32, 32, 32) })
            {
                fixture.BackColor = background;
                fixture.ForeColor = background == Color.White ? Color.Black : Color.White;
                fixture.Invalidate();
                await Task.Delay(100);
                bool observed = false;
                using (var driver = SelectionDriver(true, false, () => observed = true))
                {
                    Click("截图翻译  Ctrl+Alt+S");
                    await WaitUntil(() => observed && TranslationFinished());
                }
                ServiceVerification.Assert(ReadText(source) == fixture.SampleText,
                    "The actual Chinese small screenshot changed: " + ReadText(source));
                string translated = ReadText(result).ToLowerInvariant();
                ServiceVerification.Assert(ReadText(status).StartsWith("翻译完成")
                    && ReadText(status).Contains("中译英")
                    && System.Text.RegularExpressions.Regex.IsMatch(translated, "(not|don't).*?(turn off|shut down)"),
                    "The small Chinese screenshot lost its power-action negation.");
                Pass(background == Color.White ? "gui-small-chinese-light-screenshot" : "gui-small-chinese-dark-screenshot");
            }
            fixture.BackColor = Color.White;
            fixture.ForeColor = Color.Black;
        }

        private static async Task ExerciseChineseDirectionsAsync()
        {
            IntPtr direction = Children(window).First(h => ClassName(h).Contains("COMBOBOX"));
            foreach (int index in new[] { 0, 2 })
            {
                SendMessage(direction, 0x014E, new IntPtr(index), IntPtr.Zero);
                SetText(source, "请打开翻译窗口。");
                Click("翻译  Ctrl+Enter");
                await WaitUntil(TranslationFinished);
                ServiceVerification.Assert(ReadText(status).StartsWith("翻译完成")
                    && ReadText(status).Contains("中译英") && ReadText(result).ToLowerInvariant().Contains("window"),
                    "Chinese-to-English GUI translation failed.");
                Pass(index == 0 ? "gui-automatic-chinese-to-english" : "gui-live-chinese-to-english");
            }
        }

        private static async Task ExerciseCloseDuringTranslationAsync()
        {
            SetText(source, string.Join("\n", Enumerable.Repeat("Please do not turn off the computer during the update.", 70)));
            Click("翻译  Ctrl+Enter");
            Process worker = null;
            await WaitUntil(delegate
            {
                worker = FindInferenceWorker();
                if (worker == null) return false;
                worker.Refresh();
                if (worker.WorkingSet64 > 100L * 1024 * 1024) return true;
                worker.Dispose();
                worker = null;
                return false;
            });
            var timer = Stopwatch.StartNew();
            application.CloseMainWindow();
            await WaitUntil(() => application.HasExited && worker.HasExited);
            ServiceVerification.Assert(timer.ElapsedMilliseconds < 2000, "Closing during real model execution exceeded 2000 ms.");
            worker.Dispose();
            application.Dispose();
            application = Process.Start(@"A:\Agent\Codex\Translation\app\LightTranslate.exe");
            await WaitUntil(delegate { application.Refresh(); return application.MainWindowHandle != IntPtr.Zero; });
            window = application.MainWindowHandle;
            Pass("gui-close-during-real-model-execution-and-child-cleanup");
        }

        private static Process FindInferenceWorker()
        {
            foreach (Process process in Process.GetProcessesByName("python"))
            {
                try
                {
                    if (string.Equals(process.MainModule.FileName,
                        @"A:\Agent\Codex\Translation\app\engine\python\python.exe", StringComparison.OrdinalIgnoreCase))
                        return process;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }
            return null;
        }

        private static Timer SelectionDriver(bool select, bool rightClick, Action observed)
        {
            var timer = new Timer { Interval = 70 };
            int phase = 0;
            bool loggedSelection = false;
            bool requestedFocus = false;
            timer.Tick += delegate
            {
                IntPtr selection = FindWindow(null, "轻译 · 框选截图");
                if (selection != IntPtr.Zero && !loggedSelection)
                {
                    Console.WriteLine("Selection observed visible=" + IsWindowVisible(selection)
                        + " foreground=" + (GetForegroundWindow() == selection));
                    loggedSelection = true;
                }
                if (selection == IntPtr.Zero || !IsWindowVisible(selection)) return;
                if (!useWindowMessages && GetForegroundWindow() != selection)
                {
                    if (!requestedFocus)
                    {
                        ActivateSelectionWithMouse(selection);
                        requestedFocus = true;
                        Console.WriteLine("Physical activation click requested on the visible selection overlay.");
                    }
                    return;
                }
                if (phase == 0)
                {
                    Console.WriteLine("Selection driver select=" + select + " right=" + rightClick
                        + " visible=" + IsWindowVisible(selection) + " foreground=" + (GetForegroundWindow() == selection));
                    observed();
                    ServiceVerification.Assert(Bounds(selection).Size == SystemInformation.VirtualScreen.Size,
                        "Selection overlay is not aligned to physical desktop pixels.");
                    if (!select)
                    {
                        CancelSelection(selection, rightClick);
                        timer.Stop();
                        return;
                    }
                }
                Rectangle region = fixture.RectangleToScreen(new Rectangle(16, 30, fixture.ClientSize.Width - 32, 116));
                MoveSelection(selection, region, phase);
                if (phase == 2) timer.Stop();
                phase++;
            };
            timer.Start();
            return timer;
        }

        private static void CancelSelection(IntPtr selection, bool rightClick)
        {
            if (useWindowMessages)
            {
                if (rightClick) PostMessage(selection, 0x0204, new IntPtr(2), new IntPtr(120 | (120 << 16)));
                else PostMessage(selection, 0x0100, new IntPtr((int)Keys.Escape), IntPtr.Zero);
                return;
            }
            if (rightClick)
            {
                SetCursorPos(120, 120);
                mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero);
                mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
            }
            else { PressKey((byte)Keys.Escape, false); PressKey((byte)Keys.Escape, true); }
        }

        private static void ActivateSelectionWithMouse(IntPtr selection)
        {
            Rectangle bounds = Bounds(selection);
            SetCursorPos(bounds.Left + 3, bounds.Top + 3);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }

        private static void MoveSelection(IntPtr selection, Rectangle region, int phase)
        {
            if (useWindowMessages)
            {
                Rectangle overlay = Bounds(selection);
                Point point = phase == 0 ? new Point(region.Right, region.Bottom) : region.Location;
                IntPtr coordinate = new IntPtr((point.X - overlay.X) | ((point.Y - overlay.Y) << 16));
                PostMessage(selection, phase == 0 ? 0x0201 : phase == 1 ? 0x0200 : 0x0202,
                    new IntPtr(phase == 2 ? 0 : 1), coordinate);
                return;
            }
            if (phase == 0) { SetCursorPos(region.Right, region.Bottom); mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); }
            else if (phase == 1) SetCursorPos(region.Left, region.Top);
            else mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }

        private static bool TranslationFinished()
        {
            string text = ReadText(status);
            return IsWindowEnabled(FindButton("翻译  Ctrl+Enter"))
                && (text.StartsWith("翻译完成") || text.StartsWith("离线") || text.StartsWith("对应的离线") || text.Contains("超时"));
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            var timer = Stopwatch.StartNew();
            while (!condition())
            {
                if (timer.ElapsedMilliseconds > 25000) throw new TimeoutException("GUI condition was not reached. Status: " + ReadText(status));
                await Task.Delay(50);
            }
        }

        private static void Click(string caption)
        {
            IntPtr button = FindButton(caption);
            ServiceVerification.Assert(IsWindowEnabled(button), "GUI action was attempted before its button became enabled: " + caption);
            SendMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
        }

        private static IntPtr FindButton(string caption)
        {
            return Children(window).First(h => ClassName(h).Contains("BUTTON") && ReadText(h) == caption);
        }

        private static void Pass(string name) { passed.Add(name); Console.WriteLine("PASS " + name); }
        private static void PressKey(byte key, bool release) { keybd_event(key, 0, release ? 2u : 0u, UIntPtr.Zero); }
        private static void SetText(IntPtr control, string text) { SendMessageText(control, 0x000C, IntPtr.Zero, text); }
        private static string ReadText(IntPtr control)
        {
            if (control == IntPtr.Zero) return "";
            var text = new StringBuilder(16000);
            SendMessageBuffer(control, 0x000D, new IntPtr(text.Capacity), text);
            return text.ToString();
        }

        private static string ClassName(IntPtr control)
        {
            var name = new StringBuilder(256);
            GetClassName(control, name, name.Capacity);
            return name.ToString();
        }

        private static List<IntPtr> Children(IntPtr parent)
        {
            var children = new List<IntPtr>();
            EnumChildWindows(parent, delegate(IntPtr child, IntPtr data) { children.Add(child); return true; }, IntPtr.Zero);
            return children;
        }

        private static Rectangle Bounds(IntPtr control)
        {
            NativeRectangle value;
            GetWindowRect(control, out value);
            return Rectangle.FromLTRB(value.Left, value.Top, value.Right, value.Bottom);
        }

        private static void SaveWindowImage(string path)
        {
            Rectangle bounds = Bounds(window);
            ServiceVerification.Assert(!IsIconic(window) && bounds.Width >= 400 && bounds.Height >= 400,
                "The application window is minimized or has collapsed; screenshot evidence is invalid.");
            using (var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            using (var graphics = Graphics.FromImage(image))
            {
                IntPtr context = graphics.GetHdc();
                bool painted;
                try { painted = PrintWindow(window, context, 0); }
                finally { graphics.ReleaseHdc(context); }
                ServiceVerification.Assert(painted, "Window rendering failed.");
                image.Save(path, ImageFormat.Png);
            }
        }

        private sealed class FixtureForm : Form
        {
            internal string SampleText = "HELLO WORLD 123";
            internal float SampleFontSize = 36;
            internal string SampleFontName = "Microsoft YaHei UI";
            internal System.Drawing.Text.TextRenderingHint SampleRendering = System.Drawing.Text.TextRenderingHint.SystemDefault;
            internal FixtureForm()
            {
                Text = "Translation verification fixture";
                AutoScaleMode = AutoScaleMode.None;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Bounds = new Rectangle(80, 100, 720, 160);
                TopMost = true;
                ShowInTaskbar = false;
                BackColor = Color.White;
                ForeColor = Color.Black;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.TextRenderingHint = SampleRendering;
                using (var font = new Font(SampleFontName, SampleFontSize, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(ForeColor))
                    e.Graphics.DrawString(SampleText, font, brush, 24, 38);
            }
        }

        private delegate bool EnumWindowCallback(IntPtr window, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct NativeRectangle { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr control, StringBuilder name, int maximum);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr control, out NativeRectangle rectangle);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr control, int message, IntPtr parameter, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr context, uint flags);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMessage(IntPtr control, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageText(IntPtr control, uint message, IntPtr wParam, string text);
        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageBuffer(IntPtr control, uint message, IntPtr wParam, StringBuilder text);
    }
}
