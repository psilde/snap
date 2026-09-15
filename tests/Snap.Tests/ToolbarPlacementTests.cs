using System.Drawing;
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class ToolbarPlacementTests
{
    private static readonly Rectangle Monitor = new(0, 0, 1920, 1080);
    private static readonly Size Toolbar = new(200, 40);

    [Fact]
    public void Calculate_WhenRoomBelow_PlacesBelowSelection()
    {
        var selection = new Rectangle(100, 100, 400, 300);

        var point = ToolbarPlacement.Calculate(selection, Toolbar, Monitor, gap: 6);

        Assert.Equal(new Point(100, 406), point);
    }

    [Fact]
    public void Calculate_WhenNoRoomBelow_PlacesAboveSelection()
    {
        var selection = new Rectangle(100, 700, 400, 370);

        var point = ToolbarPlacement.Calculate(selection, Toolbar, Monitor, gap: 6);

        Assert.Equal(new Point(100, 654), point);
    }

    [Fact]
    public void Calculate_WhenSelectionFillsMonitor_PlacesInsideBottomOfSelection()
    {
        var point = ToolbarPlacement.Calculate(Monitor, Toolbar, Monitor, gap: 6);

        Assert.Equal(new Point(0, 1034), point);
        Assert.True(point.Y + Toolbar.Height <= Monitor.Bottom);
    }

    [Fact]
    public void Calculate_WhenSelectionNearRightEdge_ClampsLeftSoToolbarStaysOnMonitor()
    {
        var selection = new Rectangle(1850, 100, 70, 70);

        var point = ToolbarPlacement.Calculate(selection, Toolbar, Monitor, gap: 6);

        Assert.Equal(1720, point.X);
    }

    [Fact]
    public void Calculate_UsesMonitorBoundsNotOrigin()
    {
        var secondMonitor = new Rectangle(1920, 0, 1280, 720);

        var point = ToolbarPlacement.Calculate(secondMonitor, Toolbar, secondMonitor, gap: 6);

        Assert.Equal(new Point(1920, 674), point);
    }
}
