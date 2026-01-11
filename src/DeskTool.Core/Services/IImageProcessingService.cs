using DeskTool.Core.Models;

namespace DeskTool.Core.Services;

/// <summary>
/// Service for image preprocessing before OCR.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Convert image to grayscale.
    /// </summary>
    Task<Stream> ToGrayscaleAsync(Stream imageStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply threshold (binarization) to image.
    /// </summary>
    Task<Stream> ApplyThresholdAsync(Stream imageStream, int threshold = 128, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotate image by specified degrees.
    /// </summary>
    Task<Stream> RotateAsync(Stream imageStream, int degrees, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crop image to specified region.
    /// </summary>
    Task<Stream> CropAsync(Stream imageStream, Rectangle region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply all preprocessing steps based on options.
    /// </summary>
    Task<Stream> PreprocessAsync(Stream imageStream, OcrOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get image dimensions.
    /// </summary>
    Task<(int Width, int Height)> GetDimensionsAsync(Stream imageStream);

    /// <summary>
    /// Check if the file format is supported.
    /// </summary>
    bool IsFormatSupported(string filePath);

    /// <summary>
    /// Supported file extensions.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
