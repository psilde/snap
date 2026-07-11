using System;

namespace Snap.Services;

public static class ScreenshotFileNameGenerator
{
    public static string Generate(DateTime timestamp)
    {
        return $"screenshot_{timestamp:yyyy-MM-dd_HHmmss}.png";
    }
}
