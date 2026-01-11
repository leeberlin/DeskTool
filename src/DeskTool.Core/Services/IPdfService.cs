using DeskTool.Core.Models;

namespace DeskTool.Core.Services;

/// <summary>
/// Service for PDF manipulation operations.
/// </summary>
public interface IPdfService
{
    /// <summary>
    /// Load a PDF document.
    /// </summary>
    Task<PdfDocumentModel> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Render a page as an image.
    /// </summary>
    /// <param name="document">The PDF document.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="dpi">Resolution in DPI (default 150).</param>
    Task<Stream> RenderPageAsync(PdfDocumentModel document, int pageIndex, int dpi = 150);

    /// <summary>
    /// Generate thumbnails for all pages.
    /// </summary>
    Task<IReadOnlyList<Stream>> GenerateThumbnailsAsync(
        PdfDocumentModel document,
        int thumbnailWidth = 150,
        IProgress<PdfProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge multiple PDF files into one.
    /// </summary>
    Task<PdfMergeResult> MergeAsync(
        IEnumerable<string> inputFiles,
        string outputPath,
        PdfOperationOptions? options = null);

    /// <summary>
    /// Split a PDF by extracting specific pages.
    /// </summary>
    Task<PdfSplitResult> SplitAsync(
        string inputFile,
        string pageRange,
        string outputPath,
        PdfOperationOptions? options = null);

    /// <summary>
    /// Extract specific pages to a new PDF.
    /// </summary>
    Task<string> ExtractPagesAsync(
        PdfDocumentModel document,
        IEnumerable<int> pageNumbers,
        string outputPath,
        PdfOperationOptions? options = null);

    /// <summary>
    /// Rotate pages in a PDF.
    /// </summary>
    /// <param name="document">The PDF document.</param>
    /// <param name="pageNumbers">Pages to rotate (1-based).</param>
    /// <param name="degrees">Rotation degrees (90, 180, 270).</param>
    Task RotatePagesAsync(
        PdfDocumentModel document,
        IEnumerable<int> pageNumbers,
        int degrees);

    /// <summary>
    /// Save a document with reordered pages.
    /// </summary>
    Task SaveWithNewOrderAsync(
        PdfDocumentModel document,
        IEnumerable<int> newPageOrder,
        string outputPath);

    /// <summary>
    /// Extract text from a page (if text layer exists).
    /// </summary>
    Task<string> ExtractTextAsync(PdfDocumentModel document, int pageIndex);

    /// <summary>
    /// Extract all text from the document.
    /// </summary>
    Task<string> ExtractAllTextAsync(
        PdfDocumentModel document,
        IProgress<PdfProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a searchable PDF from a scanned document using OCR.
    /// </summary>
    Task<string> CreateSearchablePdfAsync(
        string inputFile,
        string outputPath,
        OcrOptions ocrOptions,
        IProgress<PdfProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
