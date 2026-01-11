using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTool.Core.Models;
using DeskTool.Core.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using System.Collections.ObjectModel;

namespace DeskTool.ViewModels;

/// <summary>
/// ViewModel for the PDF Tools page.
/// </summary>
public partial class PdfToolsViewModel : ObservableObject
{
    private readonly IPdfService _pdfService;
    private readonly IOcrService _ocrService;
    private readonly IFileService _fileService;
    
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private PdfDocumentModel? _currentDocument;

    [ObservableProperty]
    private bool _hasDocument;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Open a PDF file to get started";

    [ObservableProperty]
    private int _selectedPageIndex = -1;

    [ObservableProperty]
    private BitmapImage? _selectedPagePreview;

    [ObservableProperty]
    private string _extractedText = string.Empty;

    [ObservableProperty]
    private bool _hasExtractedText;

    [ObservableProperty]
    private string _pageRangeInput = string.Empty;

    // For merge operation
    public ObservableCollection<string> FilesToMerge { get; } = [];

    // Page thumbnails
    public ObservableCollection<PageThumbnailViewModel> PageThumbnails { get; } = [];

    public PdfToolsViewModel(
        IPdfService pdfService,
        IOcrService ocrService,
        IFileService fileService)
    {
        _pdfService = pdfService;
        _ocrService = ocrService;
        _fileService = fileService;
    }

    [RelayCommand]
    private async Task OpenPdfAsync()
    {
        var path = await _fileService.PickPdfFileAsync();
        if (path != null)
        {
            await LoadPdfAsync(path);
        }
    }

    public async Task LoadPdfAsync(string path)
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Loading PDF...";
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            CurrentDocument = await _pdfService.LoadAsync(path, _cancellationTokenSource.Token);
            HasDocument = true;
            
            // Generate thumbnails
            await GenerateThumbnailsAsync();
            
            StatusMessage = $"Loaded: {CurrentDocument.FileName} ({CurrentDocument.PageCount} pages)";
            Log.Information("Loaded PDF: {Path} with {PageCount} pages", path, CurrentDocument.PageCount);
            
            // Select first page
            if (PageThumbnails.Count > 0)
            {
                SelectedPageIndex = 0;
                await SelectPageAsync(0);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading PDF: {ex.Message}";
            Log.Error(ex, "Failed to load PDF: {Path}", path);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task GenerateThumbnailsAsync()
    {
        if (CurrentDocument == null) return;
        
        PageThumbnails.Clear();
        
        var progress = new Progress<PdfProgress>(p =>
        {
            Progress = (int)p.PercentComplete;
            ProgressText = $"Loading page {p.CurrentPage}/{p.TotalPages}";
        });
        
        var thumbnails = await _pdfService.GenerateThumbnailsAsync(
            CurrentDocument, 120, progress, _cancellationTokenSource?.Token ?? default);
        
        for (int i = 0; i < thumbnails.Count; i++)
        {
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(thumbnails[i].AsRandomAccessStream());
            
            PageThumbnails.Add(new PageThumbnailViewModel
            {
                PageNumber = i + 1,
                Thumbnail = bmp,
                IsSelected = i == 0
            });
        }
    }

    [RelayCommand]
    private async Task SelectPageAsync(int pageIndex)
    {
        if (CurrentDocument == null || pageIndex < 0 || pageIndex >= CurrentDocument.PageCount)
            return;

        try
        {
            SelectedPageIndex = pageIndex;
            
            // Update selection state
            foreach (var thumb in PageThumbnails)
            {
                thumb.IsSelected = thumb.PageNumber == pageIndex + 1;
            }
            
            // Load full page preview
            using var pageStream = await _pdfService.RenderPageAsync(CurrentDocument, pageIndex, 200);
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(pageStream.AsRandomAccessStream());
            SelectedPagePreview = bmp;
            
            // Try to extract text
            var text = await _pdfService.ExtractTextAsync(CurrentDocument, pageIndex);
            ExtractedText = text;
            HasExtractedText = !string.IsNullOrEmpty(text);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to select page {Index}", pageIndex);
        }
    }

    #region PDF Operations

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task RotateSelectedAsync()
    {
        if (CurrentDocument == null || SelectedPageIndex < 0) return;
        
        await _pdfService.RotatePagesAsync(CurrentDocument, [SelectedPageIndex + 1], 90);
        
        // Refresh preview
        await SelectPageAsync(SelectedPageIndex);
        StatusMessage = $"Rotated page {SelectedPageIndex + 1} by 90°";
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task ExtractTextAsync()
    {
        if (CurrentDocument == null) return;
        
        try
        {
            IsProcessing = true;
            StatusMessage = "Extracting text...";
            _cancellationTokenSource = new CancellationTokenSource();
            
            var progress = new Progress<PdfProgress>(p =>
            {
                Progress = (int)p.PercentComplete;
                ProgressText = p.Operation;
            });
            
            var text = await _pdfService.ExtractAllTextAsync(
                CurrentDocument, progress, _cancellationTokenSource.Token);
            
            ExtractedText = text;
            HasExtractedText = !string.IsNullOrEmpty(text);
            
            StatusMessage = string.IsNullOrEmpty(text) 
                ? "No text found - PDF may need OCR" 
                : $"Extracted {text.Length} characters";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Extraction cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task OcrPdfAsync()
    {
        if (CurrentDocument == null) return;
        
        try
        {
            IsProcessing = true;
            StatusMessage = "Running OCR on PDF pages...";
            _cancellationTokenSource = new CancellationTokenSource();
            
            var progress = new Progress<PdfProgress>(p =>
            {
                Progress = (int)p.PercentComplete;
                ProgressText = $"OCR page {p.CurrentPage}/{p.TotalPages}";
            });
            
            var options = new OcrOptions
            {
                Languages = [OcrLanguage.English, OcrLanguage.Vietnamese],
                PreprocessGrayscale = true
            };
            
            var outputPath = _fileService.GetTempFilePath(".pdf");
            
            await _pdfService.CreateSearchablePdfAsync(
                CurrentDocument.FilePath,
                outputPath,
                options,
                progress,
                _cancellationTokenSource.Token);
            
            // Read extracted text
            var textPath = Path.ChangeExtension(outputPath, ".txt");
            if (File.Exists(textPath))
            {
                ExtractedText = await File.ReadAllTextAsync(textPath);
                HasExtractedText = true;
            }
            
            StatusMessage = "OCR complete";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "OCR cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Log.Error(ex, "PDF OCR failed");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SplitPdfAsync()
    {
        if (CurrentDocument == null || string.IsNullOrEmpty(PageRangeInput)) return;
        
        try
        {
            var savePath = await _fileService.PickSaveFolderAsync();
            if (savePath == null) return;
            
            IsProcessing = true;
            StatusMessage = "Splitting PDF...";
            
            var outputPath = Path.Combine(savePath, 
                $"{Path.GetFileNameWithoutExtension(CurrentDocument.FilePath)}_split.pdf");
            
            var result = await _pdfService.SplitAsync(
                CurrentDocument.FilePath,
                PageRangeInput,
                outputPath,
                new PdfOperationOptions
                {
                    Progress = new Progress<PdfProgress>(p => Progress = (int)p.PercentComplete)
                });
            
            StatusMessage = result.Success 
                ? $"Split complete: {result.TotalPagesExtracted} pages" 
                : $"Split failed: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task ExtractPagesAsync()
    {
        if (CurrentDocument == null) return;
        
        var selectedPages = PageThumbnails
            .Where(t => t.IsSelected)
            .Select(t => t.PageNumber)
            .ToList();
        
        if (selectedPages.Count == 0)
        {
            StatusMessage = "Select pages to extract";
            return;
        }
        
        var savePath = await _fileService.PickSaveFolderAsync();
        if (savePath == null) return;
        
        var outputPath = Path.Combine(savePath,
            $"{Path.GetFileNameWithoutExtension(CurrentDocument.FilePath)}_extracted.pdf");
        
        await _pdfService.ExtractPagesAsync(CurrentDocument, selectedPages, outputPath);
        StatusMessage = $"Extracted {selectedPages.Count} pages";
    }

    #endregion

    #region Merge Operations

    [RelayCommand]
    private async Task AddFilesToMergeAsync()
    {
        var files = await _fileService.PickMultiplePdfFilesAsync();
        foreach (var file in files)
        {
            if (!FilesToMerge.Contains(file))
            {
                FilesToMerge.Add(file);
            }
        }
        StatusMessage = $"{FilesToMerge.Count} files ready to merge";
    }

    [RelayCommand]
    private void RemoveFileFromMerge(string filePath)
    {
        FilesToMerge.Remove(filePath);
        StatusMessage = $"{FilesToMerge.Count} files ready to merge";
    }

    [RelayCommand]
    private async Task MergeFilesAsync()
    {
        if (FilesToMerge.Count < 2)
        {
            StatusMessage = "Add at least 2 files to merge";
            return;
        }
        
        try
        {
            var savePath = await _fileService.PickSaveFolderAsync();
            if (savePath == null) return;
            
            IsProcessing = true;
            StatusMessage = "Merging PDFs...";
            
            var outputPath = Path.Combine(savePath, "merged.pdf");
            
            var result = await _pdfService.MergeAsync(
                FilesToMerge,
                outputPath,
                new PdfOperationOptions
                {
                    Progress = new Progress<PdfProgress>(p => Progress = (int)p.PercentComplete),
                    CancellationToken = _cancellationTokenSource?.Token ?? default
                });
            
            if (result.Success)
            {
                StatusMessage = $"Merged {result.SourceFileCount} files ({result.TotalPages} pages) → {Path.GetFileName(outputPath)}";
                FilesToMerge.Clear();
            }
            else
            {
                StatusMessage = $"Merge failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    #endregion

    #region Reorder

    public void ReorderPages(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex) return;
        
        PageThumbnails.Move(oldIndex, newIndex);
        
        // Update page numbers
        for (int i = 0; i < PageThumbnails.Count; i++)
        {
            PageThumbnails[i].DisplayOrder = i + 1;
        }
        
        StatusMessage = "Pages reordered - save to apply changes";
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveReorderedAsync()
    {
        if (CurrentDocument == null) return;
        
        var newOrder = PageThumbnails.Select(t => t.PageNumber).ToList();
        
        var savePath = await _fileService.PickSaveFolderAsync();
        if (savePath == null) return;
        
        var outputPath = Path.Combine(savePath,
            $"{Path.GetFileNameWithoutExtension(CurrentDocument.FilePath)}_reordered.pdf");
        
        await _pdfService.SaveWithNewOrderAsync(CurrentDocument, newOrder, outputPath);
        StatusMessage = $"Saved reordered PDF: {Path.GetFileName(outputPath)}";
    }

    #endregion

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand(CanExecute = nameof(HasExtractedText))]
    private async Task CopyTextAsync()
    {
        await _fileService.CopyToClipboardAsync(ExtractedText);
        StatusMessage = "Copied to clipboard";
    }

    [RelayCommand(CanExecute = nameof(HasExtractedText))]
    private async Task SaveTextAsync()
    {
        var suggestedName = CurrentDocument != null
            ? Path.GetFileNameWithoutExtension(CurrentDocument.FilePath) + ".txt"
            : "extracted.txt";
            
        var path = await _fileService.SaveTextFileAsync(ExtractedText, suggestedName);
        if (path != null)
        {
            StatusMessage = $"Saved: {Path.GetFileName(path)}";
        }
    }
}

/// <summary>
/// ViewModel for page thumbnails.
/// </summary>
public partial class PageThumbnailViewModel : ObservableObject
{
    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private int _displayOrder;

    [ObservableProperty]
    private BitmapImage? _thumbnail;

    [ObservableProperty]
    private bool _isSelected;
}
