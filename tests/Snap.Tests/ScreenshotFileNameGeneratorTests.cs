using System;
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class ScreenshotFileNameGeneratorTests
{
    [Fact]
    public void Generate_FormatsTimestampAsExpectedFileName()
    {
        var timestamp = new DateTime(2026, 7, 11, 14, 30, 22);

        var fileName = ScreenshotFileNameGenerator.Generate(timestamp);

        Assert.Equal("screenshot_2026-07-11_143022.png", fileName);
    }

    [Fact]
    public void Generate_PadsSingleDigitComponents()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5);

        var fileName = ScreenshotFileNameGenerator.Generate(timestamp);

        Assert.Equal("screenshot_2026-01-02_030405.png", fileName);
    }
}
