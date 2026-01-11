using DeskTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeskTool.Views;

/// <summary>
/// PDF Tools page for PDF manipulation operations.
/// </summary>
public sealed partial class PdfToolsPage : Page, IFileOpenHandler
{
    public PdfToolsViewModel ViewModel { get; }

    public PdfToolsPage()
    {
        ViewModel = App.Services.GetRequiredService<PdfToolsViewModel>();
        InitializeComponent();
    }

    public async void OpenFileAsync()
    {
        await ViewModel.OpenPdfCommand.ExecuteAsync(null);
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        
        if (e.DragUIOverride != null)
        {
            e.DragUIOverride.Caption = "Drop to open PDF";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                if (file.FileType.ToLowerInvariant() == ".pdf")
                {
                    await ViewModel.LoadPdfAsync(file.Path);
                    Log.Information("Dropped PDF file: {Path}", file.Path);
                }
            }
        }
    }

    private async void ThumbnailListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThumbnailListView.SelectedIndex >= 0)
        {
            await ViewModel.SelectPageCommand.ExecuteAsync(ThumbnailListView.SelectedIndex);
        }
    }

    private void ThumbnailListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // Handle reorder
        var items = ThumbnailListView.Items;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is PageThumbnailViewModel thumbnail)
            {
                // Find original index for this item and call reorder
                var originalIndex = ViewModel.PageThumbnails.IndexOf(thumbnail);
                if (originalIndex != i)
                {
                    ViewModel.ReorderPages(originalIndex, i);
                    break;
                }
            }
        }
    }

    private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filePath)
        {
            ViewModel.RemoveFileFromMerge(filePath);
        }
    }
}
