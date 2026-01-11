namespace DeskTool.Core.Services;

/// <summary>
/// Service for file operations.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Open file picker for images.
    /// </summary>
    Task<string?> PickImageFileAsync();

    /// <summary>
    /// Open file picker for PDF files.
    /// </summary>
    Task<string?> PickPdfFileAsync();

    /// <summary>
    /// Open file picker for multiple PDF files.
    /// </summary>
    Task<IReadOnlyList<string>> PickMultiplePdfFilesAsync();

    /// <summary>
    /// Open folder picker for save location.
    /// </summary>
    Task<string?> PickSaveFolderAsync();

    /// <summary>
    /// Save text content to file.
    /// </summary>
    Task<string?> SaveTextFileAsync(string content, string suggestedFileName = "output.txt");

    /// <summary>
    /// Save as DOCX file.
    /// </summary>
    Task<string?> SaveDocxFileAsync(string content, string suggestedFileName = "output.docx");

    /// <summary>
    /// Read file as stream.
    /// </summary>
    Task<Stream> ReadFileAsync(string filePath);

    /// <summary>
    /// Write stream to file.
    /// </summary>
    Task WriteFileAsync(string filePath, Stream content);

    /// <summary>
    /// Copy text to clipboard.
    /// </summary>
    Task CopyToClipboardAsync(string text);

    /// <summary>
    /// Get temp file path.
    /// </summary>
    string GetTempFilePath(string extension);

    /// <summary>
    /// Clean up temp files.
    /// </summary>
    Task CleanupTempFilesAsync();
}
