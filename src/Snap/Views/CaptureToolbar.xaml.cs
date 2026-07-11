using System;
using System.Windows;
using System.Windows.Controls;

namespace Snap.Views;

public partial class CaptureToolbar : UserControl
{
    public event Action? BlurClicked;
    public event Action? CopyClicked;
    public event Action? SaveClicked;
    public event Action? CancelClicked;

    public CaptureToolbar()
    {
        InitializeComponent();
    }

    private void BlurButton_Click(object sender, RoutedEventArgs e) => BlurClicked?.Invoke();
    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopyClicked?.Invoke();
    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveClicked?.Invoke();
    private void CancelButton_Click(object sender, RoutedEventArgs e) => CancelClicked?.Invoke();
}
