using System;
using System.Reflection;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Snap.Models;
using Snap.Services;
using Snap.Views;

namespace Snap;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private SettingsService? _settingsService;
    private AppSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService(SettingsService.GetDefaultConfigPath());
        _settings = _settingsService.LoadOrCreateDefault();

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.Icon = LoadIcon("tray.ico");

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += StartCapture;

        if (!_hotkeyService.Register())
        {
            _trayIcon.ShowBalloonTip(
                "Snap",
                "Ctrl+Shift+S is already in use by another app. Use the tray icon's \"New Capture\" instead.",
                BalloonIcon.Warning);
        }
    }

    public void ShowToast(string message)
    {
        _trayIcon?.ShowBalloonTip("Snap", message, BalloonIcon.Info);
    }

    internal static System.Drawing.Icon LoadIcon(string fileName)
    {
        var resourceName = $"Snap.Resources.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        return new System.Drawing.Icon(stream);
    }

    private void StartCapture()
    {
        Dispatcher.Invoke(() =>
        {
            var overlay = new OverlayWindow(_settings!, _settingsService!);
            overlay.ToastRequested += ShowToast;
            overlay.Show();
        });
    }

    private void NewCapture_Click(object sender, RoutedEventArgs e) => StartCapture();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settingsService!, _settings!);
        window.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }
}
