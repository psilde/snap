using System.Windows;
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class ScreenCaptureServiceTests
{
    [Fact]
    public void CaptureVirtualDesktop_ReturnsBitmapMatchingVirtualScreenDimensions()
    {
        var service = new ScreenCaptureService();

        using var bitmap = service.CaptureVirtualDesktop(out var bounds);

        Assert.Equal((int)SystemParameters.VirtualScreenWidth, bounds.Width);
        Assert.Equal((int)SystemParameters.VirtualScreenHeight, bounds.Height);
        Assert.Equal(bounds.Width, bitmap.Width);
        Assert.Equal(bounds.Height, bitmap.Height);
    }
}
