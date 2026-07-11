using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Snap.Models;
using Snap.Services;

namespace Snap.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    public SettingsWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        FolderTextBox.Text = _settings.SaveFolder;
        CopyPathCheckBox.IsChecked = _settings.CopyPathOnSave;

        using var iconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Snap.Resources.app.ico");
        if (iconStream is not null)
        {
            Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(FolderTextBox.Text) ? FolderTextBox.Text : AppSettings.GetDefaultSaveFolder(),
            Title = "Choose screenshot save folder"
        };

        if (dialog.ShowDialog() == true)
        {
            FolderTextBox.Text = dialog.FolderName;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SaveFolder = FolderTextBox.Text;
        _settings.CopyPathOnSave = CopyPathCheckBox.IsChecked ?? true;
        _settingsService.Save(_settings);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
