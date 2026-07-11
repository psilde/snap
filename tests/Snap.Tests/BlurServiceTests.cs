using System.Drawing;
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class BlurServiceTests
{
    private static Bitmap CreateCheckerboard(int size)
    {
        var bitmap = new Bitmap(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var isBlack = (x + y) % 2 == 0;
                bitmap.SetPixel(x, y, isBlack ? Color.Black : Color.White);
            }
        }
        return bitmap;
    }

    [Fact]
    public void ApplyBlur_LeavesPixelsOutsideRegionUnchanged()
    {
        using var source = CreateCheckerboard(8);
        var region = new Rectangle(4, 4, 4, 4);

        using var result = BlurService.ApplyBlur(source, region, radius: 2);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(source.GetPixel(x, y), result.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void ApplyBlur_ChangesPixelsInsideRegion()
    {
        using var source = CreateCheckerboard(8);
        var region = new Rectangle(2, 2, 4, 4);

        using var result = BlurService.ApplyBlur(source, region, radius: 2);

        var changedAny = false;
        for (var y = 2; y < 6; y++)
        {
            for (var x = 2; x < 6; x++)
            {
                if (source.GetPixel(x, y) != result.GetPixel(x, y))
                {
                    changedAny = true;
                }
            }
        }

        Assert.True(changedAny);
    }

    [Fact]
    public void ApplyBlur_ReturnsSameDimensionsAsSource()
    {
        using var source = CreateCheckerboard(10);

        using var result = BlurService.ApplyBlur(source, new Rectangle(0, 0, 5, 5), radius: 1);

        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
    }

    [Fact]
    public void ApplyBlur_WithZeroRadius_ReturnsUnchangedCopy()
    {
        using var source = CreateCheckerboard(4);

        using var result = BlurService.ApplyBlur(source, new Rectangle(0, 0, 4, 4), radius: 0);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(source.GetPixel(x, y), result.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void ApplyBlur_WithRegionExtendingPastEdge_ClampsToBoundsWithoutCrashing()
    {
        using var source = CreateCheckerboard(8);
        var region = new Rectangle(6, 6, 10, 10);

        using var result = BlurService.ApplyBlur(source, region, radius: 2);

        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);

        // Pixels outside the clamped intersection (x < 6 or y < 6) must be untouched.
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (x < 6 || y < 6)
                {
                    Assert.Equal(source.GetPixel(x, y), result.GetPixel(x, y));
                }
            }
        }

        // The clamped in-bounds portion of the region (x in [6,8), y in [6,8)) should
        // be affected by the blur without corrupting the bitmap or throwing.
        var changedAny = false;
        for (var y = 6; y < 8; y++)
        {
            for (var x = 6; x < 8; x++)
            {
                if (source.GetPixel(x, y) != result.GetPixel(x, y))
                {
                    changedAny = true;
                }
            }
        }

        Assert.True(changedAny);
    }

    [Fact]
    public void ApplyBlur_WithRegionEntirelyOutsideBounds_ReturnsUnchangedCopy()
    {
        using var source = CreateCheckerboard(8);
        var region = new Rectangle(20, 20, 4, 4);

        using var result = BlurService.ApplyBlur(source, region, radius: 2);

        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                Assert.Equal(source.GetPixel(x, y), result.GetPixel(x, y));
            }
        }
    }
}
