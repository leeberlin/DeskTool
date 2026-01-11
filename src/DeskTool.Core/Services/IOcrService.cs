using DeskTool.Core.Models;

namespace DeskTool.Core.Services;

/// <summary>
/// Service for OCR (Optical Character Recognition) operations.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Perform OCR on an image stream.
    /// </summary>
    /// <param name="imageStream">The image data stream.</param>
    /// <param name="options">OCR options including languages and preprocessing.</param>
    /// <param name="progress">Optional progress reporter (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OCR result with recognized text.</returns>
    Task<OcrResult> RecognizeAsync(
        Stream imageStream,
        OcrOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform OCR on an image file.
    /// </summary>
    Task<OcrResult> RecognizeFileAsync(
        string filePath,
        OcrOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available OCR languages.
    /// </summary>
    IReadOnlyList<OcrLanguage> GetAvailableLanguages();

    /// <summary>
    /// Check if a language is available.
    /// </summary>
    bool IsLanguageAvailable(OcrLanguage language);
}
