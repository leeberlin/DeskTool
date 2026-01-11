using Serilog;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DeskTool.Core.Services;

/// <summary>
/// File service implementation for Windows.
/// </summary>
public class FileService : IFileService
{
    private readonly string _tempFolder;

    public FileService()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "DeskTool");
        Directory.CreateDirectory(_tempFolder);
    }

    public async Task<string?> PickImageFileAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".tiff");
        picker.FileTypeFilter.Add(".tif");
        
        // Get the window handle for WinUI 3
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSingleFileAsync();
        
        if (file != null)
        {
            Log.Debug("Picked image file: {Path}", file.Path);
            return file.Path;
        }
        
        return null;
    }

    public async Task<string?> PickPdfFileAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        
        picker.FileTypeFilter.Add(".pdf");
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSingleFileAsync();
        
        if (file != null)
        {
            Log.Debug("Picked PDF file: {Path}", file.Path);
            return file.Path;
        }
        
        return null;
    }

    public async Task<IReadOnlyList<string>> PickMultiplePdfFilesAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        
        picker.FileTypeFilter.Add(".pdf");
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var files = await picker.PickMultipleFilesAsync();
        
        var paths = files?.Select(f => f.Path).ToList() ?? [];
        Log.Debug("Picked {Count} PDF files", paths.Count);
        
        return paths;
    }

    public async Task<string?> PickSaveFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        
        picker.FileTypeFilter.Add("*");
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> SaveTextFileAsync(string content, string suggestedFileName = "output.txt")
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        
        picker.FileTypeChoices.Add("Text File", [".txt"]);
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSaveFileAsync();
        
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content);
            Log.Information("Saved text file: {Path}", file.Path);
            return file.Path;
        }
        
        return null;
    }

    public async Task<string?> SaveDocxFileAsync(string content, string suggestedFileName = "output.docx")
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        
        picker.FileTypeChoices.Add("Word Document", [".docx"]);
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSaveFileAsync();
        
        if (file != null)
        {
            // Simple DOCX creation - for full support, use NPOI or OpenXML
            // For now, we'll create a basic text file
            // TODO: Implement proper DOCX creation with NPOI
            await FileIO.WriteTextAsync(file, content);
            Log.Information("Saved DOCX file: {Path}", file.Path);
            return file.Path;
        }
        
        return null;
    }

    public async Task<Stream> ReadFileAsync(string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        return new MemoryStream(bytes);
    }

    public async Task WriteFileAsync(string filePath, Stream content)
    {
        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream);
        Log.Debug("Wrote file: {Path}", filePath);
    }

    public async Task CopyToClipboardAsync(string text)
    {
        await Task.Run(() =>
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            Log.Debug("Copied {Length} chars to clipboard", text.Length);
        });
    }

    public string GetTempFilePath(string extension)
    {
        return Path.Combine(_tempFolder, $"{Guid.NewGuid()}{extension}");
    }

    public async Task CleanupTempFilesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var files = Directory.GetFiles(_tempFolder);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
                Log.Debug("Cleaned up {Count} temp files", files.Length);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to cleanup temp files");
            }
        });
    }
}
