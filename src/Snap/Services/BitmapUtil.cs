using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snap.Services;

public static class BitmapUtil
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            return BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                96, 96,
                PixelFormats.Bgra32,
                null,
                data.Scan0,
                data.Stride * bitmap.Height,
                data.Stride);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static Bitmap Crop(Bitmap source, Rectangle region)
    {
        return source.Clone(region, source.PixelFormat);
    }
}
