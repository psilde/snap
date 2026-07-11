using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Snap.Services;

public static class BlurService
{
    public static Bitmap ApplyBlur(Bitmap source, Rectangle region, int radius)
    {
        var result = new Bitmap(source);

        if (radius < 1)
        {
            return result;
        }

        var bounds = new Rectangle(0, 0, source.Width, source.Height);
        var clampedRegion = Rectangle.Intersect(region, bounds);

        if (clampedRegion.Width <= 0 || clampedRegion.Height <= 0)
        {
            return result;
        }

        using var crop = source.Clone(clampedRegion, source.PixelFormat);
        using var blurred = BoxBlur(crop, radius);

        using (var g = Graphics.FromImage(result))
        {
            g.DrawImage(blurred, clampedRegion.Location);
        }

        return result;
    }

    private static Bitmap BoxBlur(Bitmap source, int radius)
    {
        var width = source.Width;
        var height = source.Height;

        var srcData = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[srcData.Stride * height];
        Marshal.Copy(srcData.Scan0, pixels, 0, pixels.Length);
        source.UnlockBits(srcData);

        var stride = srcData.Stride;
        var horizontal = BoxBlurPass(pixels, width, height, stride, radius, horizontal: true);
        var both = BoxBlurPass(horizontal, width, height, stride, radius, horizontal: false);

        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(both, 0, dstData.Scan0, both.Length);
        result.UnlockBits(dstData);

        return result;
    }

    private static byte[] BoxBlurPass(byte[] pixels, int width, int height, int stride, int radius, bool horizontal)
    {
        var output = new byte[pixels.Length];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                long a = 0, r = 0, g = 0, b = 0;
                var count = 0;

                for (var offset = -radius; offset <= radius; offset++)
                {
                    var sx = horizontal ? x + offset : x;
                    var sy = horizontal ? y : y + offset;

                    if (sx < 0 || sx >= width || sy < 0 || sy >= height)
                    {
                        continue;
                    }

                    var i = sy * stride + sx * 4;
                    b += pixels[i];
                    g += pixels[i + 1];
                    r += pixels[i + 2];
                    a += pixels[i + 3];
                    count++;
                }

                var oi = y * stride + x * 4;
                output[oi] = (byte)(b / count);
                output[oi + 1] = (byte)(g / count);
                output[oi + 2] = (byte)(r / count);
                output[oi + 3] = (byte)(a / count);
            }
        }

        return output;
    }
}
