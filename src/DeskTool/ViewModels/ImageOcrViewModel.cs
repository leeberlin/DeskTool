using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTool.Core.Models;
using DeskTool.Core.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using System.Collections.ObjectModel;

namespace DeskTool.ViewModels;

/// <summary>
/// ViewModel for the Image OCR page.
/// </summary>
public partial class ImageOcrViewModel : ObservableObject
{
    private readonly IOcrService _ocrService;
    private readonly IImageProcessingService _imageService;
    private readonly IFileService _fileService;
    
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string? _imagePath;

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _statusMessage = "Drop an image or press Ctrl+O to open";

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private float _confidence;

    [ObservableProperty]
    private TimeSpan _processingTime;

    // Preprocessing options
    [ObservableProperty]
    private bool _useGrayscale = true;

    [ObservableProperty]
    private bool _useThreshold;

    [ObservableProperty]
    private int _thresholdValue = 128;

    [ObservableProperty]
    private int _rotationDegrees;

    // Crop region (null = full image)
    [ObservableProperty]
    private Rectangle? _cropRegion;

    // Language selection
    [ObservableProperty]
    private bool _useEnglish = true;

    [ObservableProperty]
    private bool _useVietnamese = true;

    [ObservableProperty]
    private bool _useGerman;

    public ObservableCollection<string> AvailableLanguages { get; } = [];

    public ImageOcrViewModel(
        IOcrService ocrService,
        IImageProcessingService imageService,
        IFileService fileService)
    {
        _ocrService = ocrService;
        _imageService = imageService;
        _fileService = fileService;

        // Check available languages
        foreach (var lang in _ocrService.GetAvailableLanguages())
        {
            AvailableLanguages.Add(lang.ToString());
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _fileService.PickImageFileAsync();
        if (path != null)
        {
            await LoadImageAsync(path);
        }
    }

    public async Task LoadImageAsync(string path)
    {
        try
        {
            if (!_imageService.IsFormatSupported(path))
            {
                StatusMessage = "Unsupported image format";
                return;
            }

            ImagePath = path;
            
            // Load preview
            PreviewImage = new BitmapImage(new Uri(path));
            HasImage = true;
            
            // Get dimensions
            using var stream = await _fileService.ReadFileAsync(path);
            var (w, h) = await _imageService.GetDimensionsAsync(stream);
            
            StatusMessage = $"Loaded: {Path.GetFileName(path)} ({w}x{h})";
            ResultText = string.Empty;
            HasResult = false;
            CropRegion = null;
            
            Log.Information("Loaded image: {Path} ({W}x{H})", path, w, h);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading image: {ex.Message}";
            Log.Error(ex, "Failed to load image: {Path}", path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunOcr))]
    private async Task RunOcrAsync()
    {
        if (ImagePath == null) return;

        IsProcessing = true;
        Progress = 0;
        StatusMessage = "Processing...";
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var options = new OcrOptions
            {
                Languages = GetSelectedLanguages(),
                PreprocessGrayscale = UseGrayscale,
                PreprocessThreshold = UseThreshold,
                ThresholdValue = ThresholdValue,
                RotationDegrees = RotationDegrees,
                CropRegion = CropRegion
            };

            var progressReporter = new Progress<int>(p => Progress = p);

            // Preprocess image first
            using var imageStream = await _fileService.ReadFileAsync(ImagePath);
            using var processed = await _imageService.PreprocessAsync(
                imageStream, options, _cancellationTokenSource.Token);

            // Run OCR
            var result = await _ocrService.RecognizeAsync(
                processed, options, progressReporter, _cancellationTokenSource.Token);

            if (result.IsSuccess)
            {
                ResultText = result.Text;
                Confidence = result.Confidence;
                ProcessingTime = result.ProcessingTime;
                HasResult = true;
                StatusMessage = $"OCR complete: {result.Words.Count} words, {result.Confidence:F1}% confidence";
            }
            else
            {
                StatusMessage = $"OCR failed: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "OCR cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Log.Error(ex, "OCR failed");
        }
        finally
        {
            IsProcessing = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private bool CanRunOcr() => HasImage && !IsProcessing;

    [RelayCommand]
    private void CancelOcr()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task CopyResultAsync()
    {
        await _fileService.CopyToClipboardAsync(ResultText);
        StatusMessage = "Copied to clipboard!";
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task SaveAsTextAsync()
    {
        var suggestedName = ImagePath != null 
            ? Path.GetFileNameWithoutExtension(ImagePath) + ".txt" 
            : "ocr_result.txt";
            
        var path = await _fileService.SaveTextFileAsync(ResultText, suggestedName);
        if (path != null)
        {
            StatusMessage = $"Saved to: {Path.GetFileName(path)}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task SaveAsDocxAsync()
    {
        var suggestedName = ImagePath != null 
            ? Path.GetFileNameWithoutExtension(ImagePath) + ".docx" 
            : "ocr_result.docx";
            
        var path = await _fileService.SaveDocxFileAsync(ResultText, suggestedName);
        if (path != null)
        {
            StatusMessage = $"Saved to: {Path.GetFileName(path)}";
        }
    }

    [RelayCommand]
    private void Rotate90()
    {
        RotationDegrees = (RotationDegrees + 90) % 360;
        StatusMessage = $"Rotation: {RotationDegrees}°";
    }

    [RelayCommand]
    private void Rotate180()
    {
        RotationDegrees = (RotationDegrees + 180) % 360;
        StatusMessage = $"Rotation: {RotationDegrees}°";
    }

    [RelayCommand]
    private void ClearCrop()
    {
        CropRegion = null;
        StatusMessage = "Crop cleared - full image will be used";
    }

    public void SetCropRegion(int x, int y, int width, int height)
    {
        CropRegion = new Rectangle(x, y, width, height);
        StatusMessage = $"Crop: ({x},{y}) {width}x{height}";
    }

    private OcrLanguage[] GetSelectedLanguages()
    {
        var languages = new List<OcrLanguage>();
        if (UseEnglish) languages.Add(OcrLanguage.English);
        if (UseVietnamese) languages.Add(OcrLanguage.Vietnamese);
        if (UseGerman) languages.Add(OcrLanguage.German);
        
        return languages.Count > 0 
            ? languages.ToArray() 
            : [OcrLanguage.English];
    }
}
