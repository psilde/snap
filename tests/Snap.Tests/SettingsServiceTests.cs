using System;
using System.IO;
using Snap.Models;
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SnapTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "settings.json");
    }

    [Fact]
    public void LoadOrCreateDefault_WhenFileMissing_CreatesFileWithDefaultSaveFolder()
    {
        var service = new SettingsService(_configPath);

        var settings = service.LoadOrCreateDefault();

        Assert.Equal(AppSettings.GetDefaultSaveFolder(), settings.SaveFolder);
        Assert.True(File.Exists(_configPath));
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsCustomSaveFolder()
    {
        var service = new SettingsService(_configPath);
        var settings = new AppSettings { SaveFolder = @"D:\CustomShots" };

        service.Save(settings);
        var reloaded = service.LoadOrCreateDefault();

        Assert.Equal(@"D:\CustomShots", reloaded.SaveFolder);
    }

    [Fact]
    public void Save_CreatesConfigDirectory_WhenMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "settings.json");
        var service = new SettingsService(nestedPath);

        service.Save(new AppSettings { SaveFolder = @"C:\Shots" });

        Assert.True(File.Exists(nestedPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
