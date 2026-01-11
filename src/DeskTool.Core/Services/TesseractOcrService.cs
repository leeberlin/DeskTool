using DeskTool.Core.Models;
using Serilog;
using System.Diagnostics;
using Tesseract;

namespace DeskTool.Core.Services;

/// <summary>
/// OCR service implementation using Tesseract OCR engine.
/// </summary>
public class TesseractOcrService : IOcrService, IDisposable
{
    private readonly string _tessDataPath;
    private readonly Dictionary<string, TesseractEngine> _engines = new();
    private readonly object _lock = new();
    private bool _disposed;

    private static readonly Dictionary<OcrLanguage, string> LanguageCodes = new()
    {
        [OcrLanguage.English] = "eng",
        [OcrLanguage.Vietnamese] = "vie",
        [OcrLanguage.German] = "deu"
    };

    public TesseractOcrService()
    {
        // Look for tessdata in app directory or use environment variable
        _tessDataPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX") 
            ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        
        Log.Information("TesseractOcrService initialized with tessdata path: {Path}", _tessDataPath);
    }

    public async Task<OcrResult> RecognizeAsync(
        Stream imageStream,
        OcrOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(10);
            
            // Convert stream to byte array for Tesseract
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms, cancellationToken);
            var imageBytes = ms.ToArray();
            
            progress?.Report(30);
            
            // Build language string (e.g., "eng+vie+deu")
            var langString = string.Join("+", 
                options.Languages.Select(l => LanguageCodes[l]));
            
            var engine = GetOrCreateEngine(langString);
            
            progress?.Report(50);
            cancellationToken.ThrowIfCancellationRequested();
            
            // Perform OCR
            var result = await Task.Run(() =>
            {
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                
                var text = page.GetText();
                var confidence = page.GetMeanConfidence() * 100;
                
                // Get word-level results
                var words = new List<OcrWord>();
                using var iter = page.GetIterator();
                iter.Begin();
                
                do
                {
                    if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
                    {
                        var wordText = iter.GetText(PageIteratorLevel.Word);
                        var wordConf = iter.GetConfidence(PageIteratorLevel.Word);
                        
                        if (!string.IsNullOrWhiteSpace(wordText))
                        {
                            words.Add(new OcrWord(
                                wordText.Trim(),
                                wordConf,
                                bounds.X1,
                                bounds.Y1,
                                bounds.Width,
                                bounds.Height));
                        }
                    }
                } while (iter.Next(PageIteratorLevel.Word));
                
                return (text, confidence, words);
            }, cancellationToken);
            
            progress?.Report(100);
            stopwatch.Stop();
            
            Log.Information("OCR completed: {CharCount} chars, {WordCount} words, {Confidence:F1}% confidence in {Time}ms",
                result.text.Length, result.words.Count, result.confidence, stopwatch.ElapsedMilliseconds);
            
            return new OcrResult
            {
                Text = result.text,
                Confidence = result.confidence,
                Words = result.words,
                DetectedLanguages = options.Languages.Select(l => l.ToString()).ToList(),
                ProcessingTime = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            Log.Information("OCR operation cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OCR failed");
            return OcrResult.Error(ex.Message);
        }
    }

    public async Task<OcrResult> RecognizeFileAsync(
        string filePath,
        OcrOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return OcrResult.Error($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        return await RecognizeAsync(stream, options, progress, cancellationToken);
    }

    public IReadOnlyList<OcrLanguage> GetAvailableLanguages()
    {
        var available = new List<OcrLanguage>();
        
        foreach (var (lang, code) in LanguageCodes)
        {
            var trainedDataPath = Path.Combine(_tessDataPath, $"{code}.traineddata");
            if (File.Exists(trainedDataPath))
            {
                available.Add(lang);
            }
        }
        
        return available;
    }

    public bool IsLanguageAvailable(OcrLanguage language)
    {
        if (!LanguageCodes.TryGetValue(language, out var code))
            return false;
            
        var trainedDataPath = Path.Combine(_tessDataPath, $"{code}.traineddata");
        return File.Exists(trainedDataPath);
    }

    private TesseractEngine GetOrCreateEngine(string langString)
    {
        lock (_lock)
        {
            if (!_engines.TryGetValue(langString, out var engine))
            {
                Log.Debug("Creating Tesseract engine for languages: {Languages}", langString);
                engine = new TesseractEngine(_tessDataPath, langString, EngineMode.Default);
                engine.SetVariable("preserve_interword_spaces", "1");
                _engines[langString] = engine;
            }
            return engine;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            foreach (var engine in _engines.Values)
            {
                engine.Dispose();
            }
            _engines.Clear();
        }
        
        _disposed = true;
        Log.Debug("TesseractOcrService disposed");
    }
}
