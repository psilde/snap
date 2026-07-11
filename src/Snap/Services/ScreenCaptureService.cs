using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;

namespace Snap.Services;

public class ScreenCaptureService
{
    public Bitmap CaptureVirtualDesktop(out Rectangle bounds)
    {
        bounds = new Rectangle(
            (int)SystemParameters.VirtualScreenLeft,
            (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.VirtualScreenWidth,
            (int)SystemParameters.VirtualScreenHeight);

        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        }

        return bitmap;
    }
}
