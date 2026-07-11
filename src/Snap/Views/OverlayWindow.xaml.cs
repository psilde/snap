using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Snap.Models;
using Snap.Services;

namespace Snap.Views;

public partial class OverlayWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ScreenCaptureService _captureService = new();

    private Bitmap _fullCapture = null!;
    private Rectangle _virtualBounds;

    private bool _isDragging;
    protected bool SelectionLocked;
    private System.Windows.Point _dragStart;
    protected Int32Rect Selection;

    public event Action<string>? ToastRequested;

    public OverlayWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        _virtualBounds = new Rectangle(
            (int)SystemParameters.VirtualScreenLeft,
            (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.VirtualScreenWidth,
            (int)SystemParameters.VirtualScreenHeight);

        Left = _virtualBounds.Left;
        Top = _virtualBounds.Top;
        Width = _virtualBounds.Width;
        Height = _virtualBounds.Height;

        Loaded += OverlayWindow_Loaded;
        MouseLeftButtonDown += OverlayWindow_MouseLeftButtonDown;
        MouseMove += OverlayWindow_MouseMove;
        MouseLeftButtonUp += OverlayWindow_MouseLeftButtonUp;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _fullCapture = _captureService.CaptureVirtualDesktop(out _virtualBounds);
        var fullSource = BitmapUtil.ToBitmapSource(_fullCapture);

        DimmedBackdrop.Source = fullSource;
        DimmedBackdrop.Opacity = 0.55;
        DimmedBackdrop.Width = _virtualBounds.Width;
        DimmedBackdrop.Height = _virtualBounds.Height;

        var dimOverlay = new System.Windows.Shapes.Rectangle
        {
            Width = _virtualBounds.Width,
            Height = _virtualBounds.Height,
            Fill = System.Windows.Media.Brushes.Black,
            Opacity = 0.35,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(dimOverlay, 0);
        Canvas.SetTop(dimOverlay, 0);
        RootCanvas.Children.Insert(1, dimOverlay);

        Activate();
        Focus();
    }

    private void OverlayWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(RootCanvas);

        if (SelectionLocked && _blurModeArmed && IsInsideSelection(position))
        {
            _isDraggingBlurBox = true;
            _blurDragStart = position;
            return;
        }

        if (SelectionLocked)
        {
            return;
        }

        _isDragging = true;
        _dragStart = position;
        SelectionBorder.Visibility = Visibility.Visible;
        RevealImage.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
    }

    private void OverlayWindow_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(RootCanvas);

        if (_isDraggingBlurBox)
        {
            UpdateBlurDragPreview(position);
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        var rect = NormalizeRect(_dragStart, position);
        DrawSelection(rect);
    }

    private void OverlayWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(RootCanvas);

        if (_isDraggingBlurBox)
        {
            _isDraggingBlurBox = false;
            CommitBlurBox(position);
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;

        var rect = NormalizeRect(_dragStart, position);

        if (rect.Width < 4 || rect.Height < 4)
        {
            ClearSelection();
            return;
        }

        Selection = new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        SelectionLocked = true;
        OnSelectionLocked(rect);
    }

    private CaptureToolbar? _toolbar;
    protected readonly List<BlurBox> BlurBoxes = new();

    private bool _blurModeArmed;
    private bool _isDraggingBlurBox;
    private System.Windows.Point _blurDragStart;
    private System.Windows.Shapes.Rectangle? _pendingBlurRect;

    private void OnSelectionLocked(Rect selectionRect)
    {
        _toolbar = new CaptureToolbar();
        _toolbar.BlurClicked += OnBlurClicked;
        _toolbar.CopyClicked += () => Export(copyToClipboard: true);
        _toolbar.SaveClicked += () => Export(copyToClipboard: false);
        _toolbar.CancelClicked += Close;

        Canvas.SetLeft(_toolbar, selectionRect.X);
        Canvas.SetTop(_toolbar, selectionRect.Y + selectionRect.Height + 6);
        RootCanvas.Children.Add(_toolbar);
    }

    private void OnBlurClicked()
    {
        _blurModeArmed = true;
    }

    private void Export(bool copyToClipboard)
    {
        using var regionCrop = BitmapUtil.Crop(FullCapture, new Rectangle(Selection.X, Selection.Y, Selection.Width, Selection.Height));
        using var final = CaptureComposer.Compose(regionCrop, BlurBoxes);

        if (copyToClipboard)
        {
            try
            {
                System.Windows.Clipboard.SetImage(BitmapUtil.ToBitmapSource(final));
                ToastRequested?.Invoke("Copied to clipboard.");
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                ToastRequested?.Invoke($"Could not copy to clipboard: {ex.Message}");
                return;
            }
        }
        else
        {
            try
            {
                Directory.CreateDirectory(_settings.SaveFolder);
                var fileName = ScreenshotFileNameGenerator.Generate(DateTime.Now);
                var path = Path.Combine(_settings.SaveFolder, fileName);
                final.Save(path, ImageFormat.Png);

                if (_settings.CopyPathOnSave)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(path);
                    }
                    catch (System.Runtime.InteropServices.ExternalException)
                    {
                        // Save itself already succeeded; a clipboard failure here is not worth
                        // blocking on or reporting as an error — the file is safely on disk.
                    }
                }

                ToastRequested?.Invoke($"Saved to {path}");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                ToastRequested?.Invoke($"Could not save screenshot: {ex.Message}");
                return;
            }
        }

        Close();
    }

    protected static Rect NormalizeRect(System.Windows.Point a, System.Windows.Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var width = Math.Abs(a.X - b.X);
        var height = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, width, height);
    }

    private bool IsInsideSelection(System.Windows.Point point)
    {
        return point.X >= Selection.X && point.X <= Selection.X + Selection.Width &&
               point.Y >= Selection.Y && point.Y <= Selection.Y + Selection.Height;
    }

    private void UpdateBlurDragPreview(System.Windows.Point current)
    {
        var rect = NormalizeRect(_blurDragStart, current);

        if (_pendingBlurRect is null)
        {
            _pendingBlurRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Yellow,
                StrokeThickness = 1,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 200, 200, 200))
            };
            RootCanvas.Children.Add(_pendingBlurRect);
        }

        Canvas.SetLeft(_pendingBlurRect, rect.X);
        Canvas.SetTop(_pendingBlurRect, rect.Y);
        _pendingBlurRect.Width = rect.Width;
        _pendingBlurRect.Height = rect.Height;
    }

    private void CommitBlurBox(System.Windows.Point end)
    {
        if (_pendingBlurRect is null)
        {
            return;
        }

        var rect = NormalizeRect(_blurDragStart, end);
        RootCanvas.Children.Remove(_pendingBlurRect);
        _pendingBlurRect = null;
        _blurModeArmed = false;

        var selectionRect = new Rect(Selection.X, Selection.Y, Selection.Width, Selection.Height);
        rect.Intersect(selectionRect);

        if (rect.Width < 4 || rect.Height < 4)
        {
            return;
        }

        var box = new BlurBox
        {
            X = (int)(rect.X - Selection.X),
            Y = (int)(rect.Y - Selection.Y),
            Width = (int)rect.Width,
            Height = (int)rect.Height,
            Radius = 12
        };

        BlurBoxes.Add(box);
        RenderBlurBox(box, rect);
    }

    private void RenderBlurBox(BlurBox box, Rect screenRect)
    {
        using var regionCrop = BitmapUtil.Crop(FullCapture, new Rectangle(Selection.X, Selection.Y, Selection.Width, Selection.Height));
        using var blurred = BlurService.ApplyBlur(regionCrop, box.ToRectangle(), box.Radius);
        using var boxCrop = BitmapUtil.Crop(blurred, box.ToRectangle());

        var preview = new System.Windows.Controls.Image
        {
            Source = BitmapUtil.ToBitmapSource(boxCrop),
            Width = box.Width,
            Height = box.Height
        };

        preview.MouseWheel += (_, args) =>
        {
            box.Radius = Math.Clamp(box.Radius + (args.Delta > 0 ? 2 : -2), 1, 40);
            RootCanvas.Children.Remove(preview);
            RenderBlurBox(box, screenRect);
            args.Handled = true;
        };

        preview.MouseRightButtonDown += (_, args) =>
        {
            BlurBoxes.Remove(box);
            RootCanvas.Children.Remove(preview);
            args.Handled = true;
        };

        Canvas.SetLeft(preview, screenRect.X);
        Canvas.SetTop(preview, screenRect.Y);
        RootCanvas.Children.Add(preview);
    }

    private void DrawSelection(Rect rect)
    {
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        var cropRegion = new Rectangle((int)rect.X, (int)rect.Y, Math.Max(1, (int)rect.Width), Math.Max(1, (int)rect.Height));
        cropRegion = Rectangle.Intersect(cropRegion, new Rectangle(0, 0, _fullCapture.Width, _fullCapture.Height));

        if (cropRegion.Width > 0 && cropRegion.Height > 0)
        {
            using var crop = BitmapUtil.Crop(_fullCapture, cropRegion);
            RevealImage.Source = BitmapUtil.ToBitmapSource(crop);
            RevealImage.Width = cropRegion.Width;
            RevealImage.Height = cropRegion.Height;
            Canvas.SetLeft(RevealImage, cropRegion.X);
            Canvas.SetTop(RevealImage, cropRegion.Y);
        }

        SizeLabel.Text = $"{(int)rect.Width} x {(int)rect.Height}";
        Canvas.SetLeft(SizeLabel, rect.X);
        Canvas.SetTop(SizeLabel, Math.Max(0, rect.Y - 20));
    }

    private void ClearSelection()
    {
        SelectionBorder.Visibility = Visibility.Collapsed;
        RevealImage.Visibility = Visibility.Collapsed;
        SizeLabel.Visibility = Visibility.Collapsed;
    }

    private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected Bitmap FullCapture => _fullCapture;
    protected Canvas Canvas => RootCanvas;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _fullCapture?.Dispose();
    }
}
