using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LightTranslate
{
    internal static class CaptureService
    {
        internal static Bitmap CaptureRegion()
        {
            Rectangle desktop = SystemInformation.VirtualScreen;
            using (var snapshot = CaptureDesktop(desktop))
            using (var selection = new SelectionForm(snapshot, desktop))
            {
                if (selection.ShowDialog() != DialogResult.OK)
                    return null;
                return snapshot.Clone(selection.SelectedRegion, PixelFormat.Format32bppArgb);
            }
        }

        internal static Bitmap CaptureDesktop(Rectangle desktop)
        {
            var image = new Bitmap(desktop.Width, desktop.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var graphics = Graphics.FromImage(image))
                    graphics.CopyFromScreen(desktop.Location, Point.Empty, desktop.Size, CopyPixelOperation.SourceCopy);
                return image;
            }
            catch { image.Dispose(); throw; }
        }
    }
}
