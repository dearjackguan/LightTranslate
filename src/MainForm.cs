using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LightTranslate
{
    internal sealed class MainForm : Form
    {
        internal static readonly Color Accent = Color.FromArgb(30, 87, 205);
        private readonly TextBox source = CreateEditor("SourceText", false);
        private readonly TextBox result = CreateEditor("ResultText", true);
        private readonly ComboBox target = new ComboBox();
        private readonly Button screenshot = CreateButton("截图翻译  Ctrl+Alt+S", "ScreenshotButton", 204, false);
        private readonly Button translate = CreateButton("翻译  Ctrl+Enter", "TranslateButton", 162, true);
        private readonly Button copy = CreateButton("复制译文", "CopyButton", 100, false);
        private readonly Button cancel = CreateButton("取消", "CancelButton", 80, false);
        private readonly Label status = CreateLabel("就绪", 9);
        private readonly Label resultLabel = CreateLabel("译文", 10);
        private bool previousResult;
        private bool busy;

        internal event Action TranslationRequested;
        internal event Action ScreenshotRequested;
        internal event Action CancellationRequested;
        internal bool HotkeyRegistered { get; private set; }
        internal string SourceText { get { return source.Text; } set { source.Text = value; } }
        internal string ResultText { get { return result.Text; } }
        internal string TargetLanguage { get { return target.SelectedIndex == 0 ? "auto" : target.SelectedIndex == 2 ? "en" : "zh-CN"; } }

        internal MainForm()
        {
            Name = "MainWindow";
            Text = "轻译";
            using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("LightTranslate.App.ico"))
                Icon = new Icon(iconStream);
            Font = new Font("Microsoft YaHei UI", 10);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(660, 550);
            MinimumSize = new Size(560, 510);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 248, 251);
            target.Name = "TargetLanguage";
            target.AccessibleName = "翻译方向，默认自动识别";
            target.DropDownStyle = ComboBoxStyle.DropDownList;
            target.Width = 130;
            target.Items.AddRange(new object[] { "自动识别", "英译中", "中译英" });
            target.SelectedIndex = 0;
            source.TextChanged += delegate { MarkResultPrevious(); };
            target.SelectedIndexChanged += delegate { MarkResultPrevious(); };
            copy.Enabled = false;
            cancel.Visible = false;
            screenshot.Click += delegate { Raise(ScreenshotRequested); };
            translate.Click += delegate { Raise(TranslationRequested); };
            cancel.Click += delegate { Raise(CancellationRequested); };
            copy.Click += delegate { CopyResult(); };
            BuildLayout();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            var header = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            header.Controls.Add(CreateLabel("轻译", 19));
            header.Controls.Add(CreateLabel("截图或输入，快速获取译文", 9));
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            tools.Controls.Add(screenshot);
            var targetLabel = CreateLabel("方向", 10);
            targetLabel.Margin = new Padding(12, 10, 8, 0);
            target.Margin = new Padding(0, 6, 0, 0);
            tools.Controls.Add(targetLabel);
            tools.Controls.Add(target);
            var editors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            editors.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            editors.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            editors.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            editors.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            editors.Controls.Add(CreateLabel("原文 · 最多 5000 字符", 10), 0, 0);
            editors.Controls.Add(source, 0, 1);
            resultLabel.Name = "ResultLabel";
            editors.Controls.Add(resultLabel, 0, 2);
            editors.Controls.Add(result, 0, 3);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0) };
            actions.Controls.AddRange(new Control[] { translate, copy, cancel });
            status.Name = "StatusText";
            status.Dock = DockStyle.Fill;
            status.AutoSize = false;
            var privacy = CreateLabel("截图识别与翻译均在本机完成，原文不上传。 · 作者：JackGuan", 8.5f);
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(tools, 0, 1);
            root.Controls.Add(editors, 0, 2);
            root.Controls.Add(actions, 0, 3);
            root.Controls.Add(status, 0, 4);
            root.Controls.Add(privacy, 0, 5);
            Controls.Add(root);
        }

        private static TextBox CreateEditor(string name, bool readOnly)
        {
            return new TextBox { Name = name, Multiline = true, ReadOnly = readOnly, MaxLength = 0,
                ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 11), BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 8), AcceptsReturn = true, AccessibleName = readOnly ? "译文" : "原文" };
        }

        private static Label CreateLabel(string text, float size)
        {
            return new Label { Text = text, AutoSize = true, Font = new Font("Microsoft YaHei UI", size),
                ForeColor = Color.FromArgb(57, 69, 88), Margin = new Padding(0, 0, 0, 4) };
        }

        private static Button CreateButton(string text, string name, int width, bool primary)
        {
            var button = new Button { Text = text, Name = name, Width = width, Height = 36,
                FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(41, 55, 76),
                Margin = new Padding(0, 0, 10, 0), UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(186, 198, 216);
            return button;
        }

        internal void SetBusy(bool value)
        {
            busy = value;
            if (value) MarkResultPrevious();
            source.ReadOnly = value;
            target.Enabled = !value;
            screenshot.Enabled = !value;
            translate.Enabled = !value;
            copy.Enabled = !value && result.TextLength > 0;
            cancel.Visible = value;
            UseWaitCursor = value;
        }

        internal void SetResult(string text)
        {
            result.Text = text;
            previousResult = false;
            resultLabel.Text = "译文";
            copy.Enabled = !busy && text.Length > 0;
        }

        private void MarkResultPrevious()
        {
            if (result.TextLength == 0) return;
            previousResult = true;
            resultLabel.Text = "译文 · 上次结果";
        }

        internal void SetStatus(string text, bool error = false)
        {
            status.Text = text;
            status.ForeColor = error ? Color.FromArgb(174, 32, 32) : Color.FromArgb(57, 69, 88);
            status.AccessibleName = text;
        }

        private void CopyResult()
        {
            if (result.TextLength == 0) return;
            try { Clipboard.SetText(result.Text); SetStatus(previousResult ? "上次译文已复制" : "译文已复制"); }
            catch (ExternalException) { SetStatus("剪贴板正被其他程序使用，请重试。", true); }
        }

        private static void Raise(Action action) { if (action != null) action(); }

        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (keys == (Keys.Control | Keys.Enter)) { if (!busy) Raise(TranslationRequested); return true; }
            if (keys == Keys.Escape && busy) { Raise(CancellationRequested); return true; }
            return base.ProcessCmdKey(ref message, keys);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            HotkeyRegistered = NativeMethods.RegisterHotKey(Handle, NativeMethods.HotkeyId, 0x4003, (uint)Keys.S);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (HotkeyRegistered) NativeMethods.UnregisterHotKey(Handle, NativeMethods.HotkeyId);
            HotkeyRegistered = false;
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.HotkeyMessage && message.WParam.ToInt32() == NativeMethods.HotkeyId)
                Raise(ScreenshotRequested);
            base.WndProc(ref message);
        }
    }
}
