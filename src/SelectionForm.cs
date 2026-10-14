using System;
using System.Drawing;
using System.Windows.Forms;

namespace LightTranslate
{
    internal sealed class SelectionForm : Form
    {
        private readonly Bitmap snapshot;
        private readonly Rectangle desktop;
        private Point origin;
        private Point current;
        private bool dragging;
        internal Rectangle SelectedRegion { get; private set; }

        internal SelectionForm(Bitmap snapshot, Rectangle desktop)
        {
            this.snapshot = snapshot;
            this.desktop = desktop;
            Name = "ScreenshotSelection";
            Text = "轻译 · 框选截图";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = desktop;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
        }

        internal static Rectangle NormalizeRegion(Point start, Point end, Size bounds)
        {
            int left = Math.Max(0, Math.Min(bounds.Width, Math.Min(start.X, end.X)));
            int top = Math.Max(0, Math.Min(bounds.Height, Math.Min(start.Y, end.Y)));
            int right = Math.Max(0, Math.Min(bounds.Width, Math.Max(start.X, end.X)));
            int bottom = Math.Max(0, Math.Min(bounds.Height, Math.Max(start.Y, end.Y)));
            return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(snapshot, 0, 0);
            using (var shade = new SolidBrush(Color.FromArgb(100, Color.Black)))
                e.Graphics.FillRectangle(shade, ClientRectangle);
            Rectangle selection = NormalizeRegion(origin, current, ClientSize);
            if (selection.Width > 0 && selection.Height > 0)
            {
                e.Graphics.DrawImage(snapshot, selection, selection, GraphicsUnit.Pixel);
                using (var border = new Pen(MainForm.Accent, 2))
                    e.Graphics.DrawRectangle(border, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);
                DrawSelectionSize(e.Graphics, selection);
            }
            Point hint = new Point(Screen.PrimaryScreen.Bounds.Left - desktop.Left + 24,
                Screen.PrimaryScreen.Bounds.Top - desktop.Top + 24);
            using (var font = new Font("Microsoft YaHei UI", 12))
                TextRenderer.DrawText(e.Graphics, "拖动选择文字区域 · Esc 或右键取消", font, hint,
                    Color.White, Color.FromArgb(35, 43, 58), TextFormatFlags.NoPadding);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (e.Button != MouseButtons.Left) return;
            origin = current = e.Location;
            dragging = true;
            Capture = true;
            Invalidate();
        }

        private void DrawSelectionSize(Graphics drawing, Rectangle selection)
        {
            string text = selection.Width + " × " + selection.Height + " 像素";
            using (var font = new Font("Microsoft YaHei UI", 9))
            {
                Size size = TextRenderer.MeasureText(text, font);
                int x = Math.Max(0, Math.Min(selection.Left, ClientSize.Width - size.Width));
                int y = selection.Bottom + 6;
                if (y + size.Height > ClientSize.Height) y = Math.Max(0, selection.Top - size.Height - 6);
                TextRenderer.DrawText(drawing, text, font, new Point(x, y), Color.White,
                    Color.FromArgb(35, 43, 58), TextFormatFlags.NoPadding);
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Capture || !dragging) return;
            dragging = false;
            origin = current = Point.Empty;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            current = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            current = e.Location;
            dragging = false;
            Capture = false;
            Rectangle selection = NormalizeRegion(origin, current, ClientSize);
            if (selection.Width < 5 || selection.Height < 5) { Invalidate(); return; }
            SelectedRegion = selection;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (keys == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref message, keys);
        }
    }
}
