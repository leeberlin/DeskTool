namespace DeskTool.Core.Models;

/// <summary>
/// Represents a PDF document with metadata and pages.
/// </summary>
public class PdfDocumentModel : IDisposable
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public int PageCount { get; init; }
    public bool IsLoaded { get; private set; } = true;
    public bool HasTextLayer { get; init; }
    
    /// <summary>
    /// Page models for each page in the document.
    /// </summary>
    public IReadOnlyList<PdfPageModel> Pages { get; init; } = [];
    
    /// <summary>
    /// Internal reference to the PDFsharp document.
    /// </summary>
    internal object? InternalDocument { get; set; }
    
    public void Dispose()
    {
        if (InternalDocument is IDisposable disposable)
        {
            disposable.Dispose();
        }
        IsLoaded = false;
    }
}

/// <summary>
/// Represents a single page in a PDF document.
/// </summary>
public class PdfPageModel
{
    public int PageNumber { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public int Rotation { get; set; }
    public bool HasText { get; init; }
    
    /// <summary>
    /// Cached thumbnail image path.
    /// </summary>
    public string? ThumbnailPath { get; set; }
    
    /// <summary>
    /// Extracted or OCR'd text for this page.
    /// </summary>
    public string? Text { get; set; }
}

/// <summary>
/// Options for PDF operations.
/// </summary>
public class PdfOperationOptions
{
    public IProgress<PdfProgress>? Progress { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Progress information for PDF operations.
/// </summary>
public record PdfProgress(
    int CurrentPage,
    int TotalPages,
    string Operation,
    double PercentComplete
)
{
    public static PdfProgress Start(int total, string op) => new(0, total, op, 0);
    public PdfProgress Next() => this with { CurrentPage = CurrentPage + 1, PercentComplete = (CurrentPage + 1) * 100.0 / TotalPages };
}

/// <summary>
/// Range specification for PDF page operations.
/// </summary>
public record PageRange
{
    public int Start { get; init; }
    public int End { get; init; }
    
    /// <summary>
    /// Parse a range string like "1-5" or "3" or "1,3,5-7".
    /// </summary>
    public static IEnumerable<int> Parse(string rangeString, int maxPages)
    {
        var pages = new HashSet<int>();
        var parts = rangeString.Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-'))
            {
                var range = trimmed.Split('-');
                if (range.Length == 2 && 
                    int.TryParse(range[0], out int start) && 
                    int.TryParse(range[1], out int end))
                {
                    for (int i = Math.Max(1, start); i <= Math.Min(maxPages, end); i++)
                    {
                        pages.Add(i);
                    }
                }
            }
            else if (int.TryParse(trimmed, out int page) && page >= 1 && page <= maxPages)
            {
                pages.Add(page);
            }
        }
        
        return pages.OrderBy(p => p);
    }
    
    public static PageRange All(int maxPages) => new() { Start = 1, End = maxPages };
}

/// <summary>
/// Result of a PDF merge operation.
/// </summary>
public record PdfMergeResult(
    string OutputPath,
    int TotalPages,
    int SourceFileCount,
    bool Success,
    string? ErrorMessage = null
);

/// <summary>
/// Result of a PDF split operation.
/// </summary>
public record PdfSplitResult(
    IReadOnlyList<string> OutputPaths,
    int TotalPagesExtracted,
    bool Success,
    string? ErrorMessage = null
);
