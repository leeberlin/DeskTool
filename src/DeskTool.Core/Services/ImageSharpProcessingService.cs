using DeskTool.Core.Models;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DeskTool.Core.Services;

/// <summary>
/// Image processing service using ImageSharp.
/// </summary>
public class ImageSharpProcessingService : IImageProcessingService
{
    public IReadOnlyList<string> SupportedExtensions => [".png", ".jpg", ".jpeg", ".webp", ".tiff", ".tif", ".bmp"];

    public async Task<Stream> ToGrayscaleAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        
        image.Mutate(ctx => ctx.Grayscale());
        
        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        
        Log.Debug("Converted image to grayscale");
        return output;
    }

    public async Task<Stream> ApplyThresholdAsync(Stream imageStream, int threshold = 128, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        
        image.Mutate(ctx => ctx
            .Grayscale()
            .BinaryThreshold(threshold / 255f));
        
        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        
        Log.Debug("Applied threshold {Threshold} to image", threshold);
        return output;
    }

    public async Task<Stream> RotateAsync(Stream imageStream, int degrees, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        
        image.Mutate(ctx => ctx.Rotate(degrees));
        
        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        
        Log.Debug("Rotated image by {Degrees} degrees", degrees);
        return output;
    }

    public async Task<Stream> CropAsync(Stream imageStream, Models.Rectangle region, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        
        var cropRect = new SixLabors.ImageSharp.Rectangle(region.X, region.Y, region.Width, region.Height);
        image.Mutate(ctx => ctx.Crop(cropRect));
        
        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        
        Log.Debug("Cropped image to region ({X},{Y},{W},{H})", region.X, region.Y, region.Width, region.Height);
        return output;
    }

    public async Task<Stream> PreprocessAsync(Stream imageStream, OcrOptions options, CancellationToken cancellationToken = default)
    {
        // Read image once
        var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        
        using var image = await Image.LoadAsync<Rgba32>(ms, cancellationToken);
        
        // Apply crop first if specified
        if (options.CropRegion.HasValue)
        {
            var r = options.CropRegion.Value;
            var cropRect = new SixLabors.ImageSharp.Rectangle(r.X, r.Y, r.Width, r.Height);
            image.Mutate(ctx => ctx.Crop(cropRect));
        }
        
        // Apply rotation
        if (options.RotationDegrees != 0)
        {
            image.Mutate(ctx => ctx.Rotate(options.RotationDegrees));
        }
        
        // Apply grayscale
        if (options.PreprocessGrayscale)
        {
            image.Mutate(ctx => ctx.Grayscale());
        }
        
        // Apply threshold
        if (options.PreprocessThreshold)
        {
            image.Mutate(ctx => ctx.BinaryThreshold(options.ThresholdValue / 255f));
        }
        
        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        
        Log.Debug("Preprocessed image with options: Grayscale={Gray}, Threshold={Thresh}, Rotation={Rot}",
            options.PreprocessGrayscale, options.PreprocessThreshold, options.RotationDegrees);
        
        return output;
    }

    public async Task<(int Width, int Height)> GetDimensionsAsync(Stream imageStream)
    {
        var info = await Image.IdentifyAsync(imageStream);
        imageStream.Position = 0;
        return (info.Width, info.Height);
    }

    public bool IsFormatSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
}
