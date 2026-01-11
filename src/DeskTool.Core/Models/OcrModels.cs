namespace DeskTool.Core.Models;

/// <summary>
/// Result of an OCR operation.
/// </summary>
public class OcrResult
{
    /// <summary>
    /// The recognized text.
    /// </summary>
    public string Text { get; init; } = string.Empty;
    
    /// <summary>
    /// Confidence score (0-100).
    /// </summary>
    public float Confidence { get; init; }
    
    /// <summary>
    /// Languages detected in the text.
    /// </summary>
    public IReadOnlyList<string> DetectedLanguages { get; init; } = [];
    
    /// <summary>
    /// Time taken for OCR processing.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }
    
    /// <summary>
    /// Individual word results with bounding boxes.
    /// </summary>
    public IReadOnlyList<OcrWord> Words { get; init; } = [];
    
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess => !string.IsNullOrEmpty(Text);
    
    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    public static OcrResult Empty => new() { Text = string.Empty };
    
    public static OcrResult Error(string message) => new() { ErrorMessage = message };
}

/// <summary>
/// Individual word from OCR with position and confidence.
/// </summary>
public record OcrWord(
    string Text,
    float Confidence,
    int X,
    int Y,
    int Width,
    int Height
);

/// <summary>
/// Languages supported for OCR.
/// </summary>
public enum OcrLanguage
{
    English,
    Vietnamese,
    German
}

/// <summary>
/// Options for OCR processing.
/// </summary>
public class OcrOptions
{
    public OcrLanguage[] Languages { get; init; } = [OcrLanguage.English];
    public bool PreprocessGrayscale { get; init; } = true;
    public bool PreprocessThreshold { get; init; } = false;
    public int ThresholdValue { get; init; } = 128;
    public int RotationDegrees { get; init; } = 0; // 0, 90, 180, 270
    public Rectangle? CropRegion { get; init; }
}

/// <summary>
/// Simple rectangle structure for crop regions.
/// </summary>
public record struct Rectangle(int X, int Y, int Width, int Height);
