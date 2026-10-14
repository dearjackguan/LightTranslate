using System.Drawing;
using System.Drawing.Imaging;

namespace LightTranslate
{
    internal static class OcrImagePreparation
    {
        internal static void Save(Bitmap image, string path)
        {
            if (!HasDarkBorder(image)) { image.Save(path, ImageFormat.Png); return; }
            using (var prepared = new Bitmap(image.Width, image.Height))
            using (var drawing = Graphics.FromImage(prepared))
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix();
                matrix.Matrix00 = matrix.Matrix11 = matrix.Matrix22 = -1;
                matrix.Matrix40 = matrix.Matrix41 = matrix.Matrix42 = 1;
                attributes.SetColorMatrix(matrix);
                drawing.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                prepared.Save(path, ImageFormat.Png);
            }
        }
        private static bool HasDarkBorder(Bitmap image)
        {
            int dark = 0;
            for (int i = 0; i < 16; i++)
            {
                int x = i * (image.Width - 1) / 15, y = i * (image.Height - 1) / 15;
                dark += IsDark(image.GetPixel(x, 0)) + IsDark(image.GetPixel(x, image.Height - 1));
                dark += IsDark(image.GetPixel(0, y)) + IsDark(image.GetPixel(image.Width - 1, y));
            }
            return dark >= 48;
        }
        private static int IsDark(Color color)
        { return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B < 96 ? 1 : 0; }
    }
}
