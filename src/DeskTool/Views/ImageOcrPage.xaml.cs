using DeskTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Serilog;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeskTool.Views;

/// <summary>
/// Image OCR page for loading images and performing OCR.
/// </summary>
public sealed partial class ImageOcrPage : Page, IFileOpenHandler
{
    public ImageOcrViewModel ViewModel { get; }
    
    // Crop selection state
    private bool _isSelecting;
    private Windows.Foundation.Point _selectionStart;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _selectionRect;

    public ImageOcrPage()
    {
        ViewModel = App.Services.GetRequiredService<ImageOcrViewModel>();
        InitializeComponent();
        
        // Set up keyboard handler for Ctrl+C
        ResultTextBox.KeyDown += ResultTextBox_KeyDown;
    }

    public async void OpenFileAsync()
    {
        await ViewModel.OpenFileCommand.ExecuteAsync(null);
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        
        if (e.DragUIOverride != null)
        {
            e.DragUIOverride.Caption = "Drop to open";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                var ext = file.FileType.ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".tiff" or ".tif")
                {
                    await ViewModel.LoadImageAsync(file.Path);
                    Log.Information("Dropped image file: {Path}", file.Path);
                }
            }
        }
    }

    private void ResultTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Handle Ctrl+C for copying from result
        if (e.Key == Windows.System.VirtualKey.C)
        {
            var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            
            if (ctrlPressed && !string.IsNullOrEmpty(ViewModel.ResultText))
            {
                // If there's a selection, the default copy will work
                // If no selection, copy all text
                if (string.IsNullOrEmpty(ResultTextBox.SelectedText))
                {
                    ViewModel.CopyResultCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }

    #region Crop Selection

    private void CropCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.HasImage) return;
        
        _isSelecting = true;
        _selectionStart = e.GetCurrentPoint(CropCanvas).Position;
        
        CropCanvas.CapturePointer(e.Pointer);
        
        // Create selection rectangle
        _selectionRect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            StrokeThickness = 2,
            StrokeDashArray = [5, 2],
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(50, 30, 144, 255))
        };
        
        Canvas.SetLeft(_selectionRect, _selectionStart.X);
        Canvas.SetTop(_selectionRect, _selectionStart.Y);
        CropCanvas.Children.Add(_selectionRect);
    }

    private void CropCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting || _selectionRect == null) return;
        
        var current = e.GetCurrentPoint(CropCanvas).Position;
        
        var x = Math.Min(_selectionStart.X, current.X);
        var y = Math.Min(_selectionStart.Y, current.Y);
        var w = Math.Abs(current.X - _selectionStart.X);
        var h = Math.Abs(current.Y - _selectionStart.Y);
        
        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;
    }

    private void CropCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting || _selectionRect == null) return;
        
        _isSelecting = false;
        CropCanvas.ReleasePointerCapture(e.Pointer);
        
        // Get final selection
        var x = (int)Canvas.GetLeft(_selectionRect);
        var y = (int)Canvas.GetTop(_selectionRect);
        var w = (int)_selectionRect.Width;
        var h = (int)_selectionRect.Height;
        
        if (w > 10 && h > 10)
        {
            // Set crop region in ViewModel
            // Note: Need to scale to actual image coordinates
            ViewModel.SetCropRegion(x, y, w, h);
        }
        else
        {
            // Too small, clear selection
            CropCanvas.Children.Remove(_selectionRect);
            _selectionRect = null;
        }
    }

    #endregion
}
