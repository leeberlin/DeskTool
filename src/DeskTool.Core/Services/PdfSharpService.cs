using DeskTool.Core.Models;
using PDFtoImage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using Serilog;
using SkiaSharp;
using System.Text;

namespace DeskTool.Core.Services;

/// <summary>
/// PDF service implementation using PDFsharp and PDFtoImage.
/// </summary>
public class PdfSharpService : IPdfService
{
    private readonly IOcrService _ocrService;

    public PdfSharpService(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<PdfDocumentModel> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            Log.Information("Loading PDF: {FilePath}", filePath);
            
            var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.Modify);
            var hasTextLayer = CheckForTextLayer(doc);
            
            var pages = new List<PdfPageModel>();
            for (int i = 0; i < doc.PageCount; i++)
            {
                var page = doc.Pages[i];
                pages.Add(new PdfPageModel
                {
                    PageNumber = i + 1,
                    Width = page.Width.Point,
                    Height = page.Height.Point,
                    Rotation = (int)page.Rotate,
                    HasText = hasTextLayer
                });
            }
            
            Log.Information("PDF loaded: {PageCount} pages, HasText: {HasText}", doc.PageCount, hasTextLayer);
            
            return new PdfDocumentModel
            {
                FilePath = filePath,
                PageCount = doc.PageCount,
                HasTextLayer = hasTextLayer,
                Pages = pages,
                InternalDocument = doc
            };
        }, cancellationToken);
    }

    public async Task<Stream> RenderPageAsync(PdfDocumentModel document, int pageIndex, int dpi = 150)
    {
        return await Task.Run(() =>
        {
            var options = new RenderOptions(Dpi: dpi);
            using var bitmap = Conversion.ToImage(document.FilePath, pageIndex, options: options);
            var ms = new MemoryStream();
            bitmap.Encode(ms, SKEncodedImageFormat.Png, 90);
            ms.Position = 0;
            return (Stream)ms;
        });
    }

    public async Task<IReadOnlyList<Stream>> GenerateThumbnailsAsync(
        PdfDocumentModel document,
        int thumbnailWidth = 150,
        IProgress<PdfProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var thumbnails = new List<Stream>();
        var total = document.PageCount;
        
        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var ms = new MemoryStream();
            await Task.Run(() =>
            {
                // Calculate DPI to achieve target width
                var page = document.Pages[i];
                var dpi = (int)(thumbnailWidth / page.Width * 72);
                dpi = Math.Max(36, Math.Min(dpi, 150)); // Clamp between 36-150
                
                var options = new RenderOptions(Dpi: dpi);
                using var bitmap = Conversion.ToImage(document.FilePath, i, options: options);
                bitmap.Encode(ms, SKEncodedImageFormat.Png, 80);
            }, cancellationToken);
            
            ms.Position = 0;
            thumbnails.Add(ms);
            
            progress?.Report(new PdfProgress(i + 1, total, "Generating thumbnails", (i + 1) * 100.0 / total));
        }
        
        Log.Debug("Generated {Count} thumbnails", thumbnails.Count);
        return thumbnails;
    }

    public async Task<PdfMergeResult> MergeAsync(
        IEnumerable<string> inputFiles,
        string outputPath,
        PdfOperationOptions? options = null)
    {
        var files = inputFiles.ToList();
        var ct = options?.CancellationToken ?? default;
        
        return await Task.Run(() =>
        {
            try
            {
                Log.Information("Merging {Count} PDF files to {Output}", files.Count, outputPath);
                
                using var outputDoc = new PdfDocument();
                int totalPages = 0;
                int fileIndex = 0;
                
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    using var inputDoc = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                    
                    foreach (var page in inputDoc.Pages)
                    {
                        outputDoc.AddPage(page);
                        totalPages++;
                    }
                    
                    fileIndex++;
                    options?.Progress?.Report(new PdfProgress(
                        fileIndex, files.Count, "Merging PDFs", fileIndex * 100.0 / files.Count));
                }
                
                outputDoc.Save(outputPath);
                
                Log.Information("Merge complete: {TotalPages} pages from {FileCount} files", totalPages, files.Count);
                
                return new PdfMergeResult(outputPath, totalPages, files.Count, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PDF merge failed");
                return new PdfMergeResult(outputPath, 0, files.Count, false, ex.Message);
            }
        }, ct);
    }

    public async Task<PdfSplitResult> SplitAsync(
        string inputFile,
        string pageRange,
        string outputPath,
        PdfOperationOptions? options = null)
    {
        var ct = options?.CancellationToken ?? default;
        
        return await Task.Run(() =>
        {
            try
            {
                using var inputDoc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import);
                var pages = PageRange.Parse(pageRange, inputDoc.PageCount).ToList();
                
                Log.Information("Splitting PDF: extracting pages {Range} to {Output}", pageRange, outputPath);
                
                using var outputDoc = new PdfDocument();
                
                foreach (var pageNum in pages)
                {
                    ct.ThrowIfCancellationRequested();
                    outputDoc.AddPage(inputDoc.Pages[pageNum - 1]);
                }
                
                outputDoc.Save(outputPath);
                
                return new PdfSplitResult([outputPath], pages.Count, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PDF split failed");
                return new PdfSplitResult([], 0, false, ex.Message);
            }
        }, ct);
    }

    public async Task<string> ExtractPagesAsync(
        PdfDocumentModel document,
        IEnumerable<int> pageNumbers,
        string outputPath,
        PdfOperationOptions? options = null)
    {
        var pages = pageNumbers.ToList();
        var ct = options?.CancellationToken ?? default;
        
        return await Task.Run(() =>
        {
            using var inputDoc = PdfReader.Open(document.FilePath, PdfDocumentOpenMode.Import);
            using var outputDoc = new PdfDocument();
            
            foreach (var pageNum in pages)
            {
                ct.ThrowIfCancellationRequested();
                if (pageNum >= 1 && pageNum <= inputDoc.PageCount)
                {
                    outputDoc.AddPage(inputDoc.Pages[pageNum - 1]);
                }
            }
            
            outputDoc.Save(outputPath);
            Log.Information("Extracted {Count} pages to {Output}", pages.Count, outputPath);
            
            return outputPath;
        }, ct);
    }

    public async Task RotatePagesAsync(
        PdfDocumentModel document,
        IEnumerable<int> pageNumbers,
        int degrees)
    {
        await Task.Run(() =>
        {
            if (document.InternalDocument is not PdfDocument doc)
            {
                throw new InvalidOperationException("Document not loaded properly");
            }
            
            foreach (var pageNum in pageNumbers)
            {
                if (pageNum >= 1 && pageNum <= doc.PageCount)
                {
                    var page = doc.Pages[pageNum - 1];
                    page.Rotate = (page.Rotate + degrees) % 360;
                    
                    // Update model
                    document.Pages[pageNum - 1].Rotation = (int)page.Rotate;
                }
            }
            
            Log.Debug("Rotated pages {Pages} by {Degrees} degrees", pageNumbers, degrees);
        });
    }

    public async Task SaveWithNewOrderAsync(
        PdfDocumentModel document,
        IEnumerable<int> newPageOrder,
        string outputPath)
    {
        var order = newPageOrder.ToList();
        
        await Task.Run(() =>
        {
            using var inputDoc = PdfReader.Open(document.FilePath, PdfDocumentOpenMode.Import);
            using var outputDoc = new PdfDocument();
            
            foreach (var pageNum in order)
            {
                if (pageNum >= 1 && pageNum <= inputDoc.PageCount)
                {
                    outputDoc.AddPage(inputDoc.Pages[pageNum - 1]);
                }
            }
            
            outputDoc.Save(outputPath);
            Log.Information("Saved reordered PDF ({Count} pages) to {Output}", order.Count, outputPath);
        });
    }

    public async Task<string> ExtractTextAsync(PdfDocumentModel document, int pageIndex)
    {
        return await Task.Run(() =>
        {
            if (document.InternalDocument is not PdfDocument doc)
            {
                return string.Empty;
            }
            
            if (pageIndex < 0 || pageIndex >= doc.PageCount)
            {
                return string.Empty;
            }
            
            var page = doc.Pages[pageIndex];
            return ExtractTextFromPage(page);
        });
    }

    public async Task<string> ExtractAllTextAsync(
        PdfDocumentModel document,
        IProgress<PdfProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        
        for (int i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var text = await ExtractTextAsync(document, i);
            
            if (!string.IsNullOrEmpty(text))
            {
                sb.AppendLine($"--- Page {i + 1} ---");
                sb.AppendLine(text);
                sb.AppendLine();
            }
            
            progress?.Report(new PdfProgress(i + 1, document.PageCount, "Extracting text", (i + 1) * 100.0 / document.PageCount));
        }
        
        return sb.ToString();
    }

    public async Task<string> CreateSearchablePdfAsync(
        string inputFile,
        string outputPath,
        OcrOptions ocrOptions,
        IProgress<PdfProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Creating searchable PDF from {Input}", inputFile);
        
        using var doc = await LoadAsync(inputFile, cancellationToken);
        var sb = new StringBuilder();
        
        for (int i = 0; i < doc.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            progress?.Report(new PdfProgress(i + 1, doc.PageCount, "OCR processing", (i + 1) * 100.0 / doc.PageCount));
            
            // Render page to image
            using var pageImage = await RenderPageAsync(doc, i, 300);
            
            // OCR the page
            var ocrResult = await _ocrService.RecognizeAsync(pageImage, ocrOptions, null, cancellationToken);
            
            sb.AppendLine($"--- Page {i + 1} ---");
            sb.AppendLine(ocrResult.Text);
            sb.AppendLine();
            
            doc.Pages[i].Text = ocrResult.Text;
        }
        
        // Copy original and save text separately
        File.Copy(inputFile, outputPath, true);
        
        var textPath = Path.ChangeExtension(outputPath, ".txt");
        await File.WriteAllTextAsync(textPath, sb.ToString(), cancellationToken);
        
        Log.Information("Searchable PDF created: {Output} with text file: {TextPath}", outputPath, textPath);
        
        return outputPath;
    }

    private static bool CheckForTextLayer(PdfDocument doc)
    {
        if (doc.PageCount == 0) return false;
        
        var text = ExtractTextFromPage(doc.Pages[0]);
        return !string.IsNullOrWhiteSpace(text);
    }

    private static string ExtractTextFromPage(PdfPage page)
    {
        try
        {
            var content = ContentReader.ReadContent(page);
            var sb = new StringBuilder();
            ExtractText(content, sb);
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void ExtractText(CObject obj, StringBuilder sb)
    {
        if (obj is COperator op)
        {
            if (op.OpCode.Name == "Tj" || op.OpCode.Name == "TJ")
            {
                foreach (var operand in op.Operands)
                {
                    ExtractText(operand, sb);
                }
            }
        }
        else if (obj is CString str)
        {
            sb.Append(str.Value);
        }
        else if (obj is CArray arr)
        {
            foreach (var item in arr)
            {
                ExtractText(item, sb);
            }
        }
        else if (obj is CSequence seq)
        {
            foreach (var item in seq)
            {
                ExtractText(item, sb);
            }
        }
    }
}
