using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Snap.Services;

public static class ToolbarPlacement
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// Picks where to put the toolbar so it stays on the given monitor: below the selection if it
    /// fits, otherwise above it, otherwise tucked inside the bottom of the selection.
    /// </summary>
    public static Point Calculate(Rectangle selection, Size toolbar, Rectangle monitor, int gap)
    {
        int y;
        if (selection.Bottom + gap + toolbar.Height <= monitor.Bottom)
        {
            y = selection.Bottom + gap;
        }
        else if (selection.Top - gap - toolbar.Height >= monitor.Top)
        {
            y = selection.Top - gap - toolbar.Height;
        }
        else
        {
            y = selection.Bottom - gap - toolbar.Height;
        }

        y = Math.Max(monitor.Top, Math.Min(y, monitor.Bottom - toolbar.Height));
        var x = Math.Max(monitor.Left, Math.Min(selection.Left, monitor.Right - toolbar.Width));

        return new Point(x, y);
    }

    /// <summary>Screen bounds of the monitor that contains most of <paramref name="screenRect"/>.</summary>
    public static Rectangle GetMonitorBounds(Rectangle screenRect)
    {
        var rect = new RECT { Left = screenRect.Left, Top = screenRect.Top, Right = screenRect.Right, Bottom = screenRect.Bottom };
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfo(MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST), ref info))
        {
            return screenRect;
        }

        var m = info.rcMonitor;
        return Rectangle.FromLTRB(m.Left, m.Top, m.Right, m.Bottom);
    }
}
