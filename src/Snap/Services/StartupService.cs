using System;
using Microsoft.Win32;

namespace Snap.Services;

public class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Snap";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var stored = key?.GetValue(ValueName) as string;
        return PathsMatch(stored, GetExecutablePath());
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, GetExecutablePath());
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool PathsMatch(string? stored, string current)
    {
        return !string.IsNullOrEmpty(stored) && string.Equals(stored, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine the current executable path.");
    }
}
