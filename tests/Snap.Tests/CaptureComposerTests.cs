using System.Collections.Generic;
using System.Drawing;
using Snap.Models;
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class CaptureComposerTests
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
    public void Compose_WithNoBlurBoxes_ReturnsPixelIdenticalCopy()
    {
        using var source = CreateCheckerboard(6);

        using var result = CaptureComposer.Compose(source, new List<BlurBox>());

        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                Assert.Equal(source.GetPixel(x, y), result.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void Compose_WithOneBlurBox_ChangesOnlyThatRegion()
    {
        using var source = CreateCheckerboard(8);
        var boxes = new List<BlurBox>
        {
            new() { X = 2, Y = 2, Width = 4, Height = 4, Radius = 2 }
        };

        using var result = CaptureComposer.Compose(source, boxes);

        Assert.Equal(source.GetPixel(0, 0), result.GetPixel(0, 0));

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
    public void Compose_WithMultipleBlurBoxes_AppliesAllOfThem()
    {
        using var source = CreateCheckerboard(10);
        var boxes = new List<BlurBox>
        {
            new() { X = 0, Y = 0, Width = 3, Height = 3, Radius = 2 },
            new() { X = 6, Y = 6, Width = 3, Height = 3, Radius = 2 }
        };

        using var result = CaptureComposer.Compose(source, boxes);

        var firstBoxChanged = false;
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 3; x++)
            if (source.GetPixel(x, y) != result.GetPixel(x, y)) firstBoxChanged = true;

        var secondBoxChanged = false;
        for (var y = 6; y < 9; y++)
        for (var x = 6; x < 9; x++)
            if (source.GetPixel(x, y) != result.GetPixel(x, y)) secondBoxChanged = true;

        Assert.True(firstBoxChanged);
        Assert.True(secondBoxChanged);
    }
}
