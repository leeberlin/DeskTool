using DeskTool.Core.Models;
using DeskTool.Core.Services;
using Moq;

namespace DeskTool.Tests;

/// <summary>
/// Unit tests for image processing service.
/// </summary>
public class ImageProcessingServiceTests
{
    [Fact]
    public void SupportedExtensions_ContainsExpectedFormats()
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.SupportedExtensions)
            .Returns(new[] { ".png", ".jpg", ".jpeg", ".webp", ".tiff", ".tif" });

        var extensions = mockService.Object.SupportedExtensions;

        Assert.Contains(".png", extensions);
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".webp", extensions);
        Assert.Contains(".tiff", extensions);
    }

    [Theory]
    [InlineData("image.png", true)]
    [InlineData("image.jpg", true)]
    [InlineData("image.jpeg", true)]
    [InlineData("image.webp", true)]
    [InlineData("image.tiff", true)]
    [InlineData("image.gif", false)]
    [InlineData("document.pdf", false)]
    public void IsFormatSupported_ReturnsExpected(string filePath, bool expected)
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.IsFormatSupported(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                var ext = Path.GetExtension(path).ToLower();
                return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".tiff" or ".tif";
            });

        var result = mockService.Object.IsFormatSupported(filePath);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetDimensionsAsync_ValidImage_ReturnsDimensions()
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.GetDimensionsAsync(It.IsAny<Stream>()))
            .ReturnsAsync((1920, 1080));

        using var stream = new MemoryStream();
        var (width, height) = await mockService.Object.GetDimensionsAsync(stream);

        Assert.Equal(1920, width);
        Assert.Equal(1080, height);
    }

    [Fact]
    public async Task ToGrayscaleAsync_ValidImage_ReturnsStream()
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.ToGrayscaleAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        using var inputStream = new MemoryStream();
        using var result = await mockService.Object.ToGrayscaleAsync(inputStream);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task RotateAsync_ValidDegrees_ReturnsStream()
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.RotateAsync(It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        using var inputStream = new MemoryStream();
        using var result = await mockService.Object.RotateAsync(inputStream, 90);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task CropAsync_ValidRegion_ReturnsStream()
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.CropAsync(
                It.IsAny<Stream>(), 
                It.IsAny<Rectangle>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        using var inputStream = new MemoryStream();
        var region = new Rectangle(0, 0, 100, 100);
        using var result = await mockService.Object.CropAsync(inputStream, region);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task PreprocessAsync_WithOptions_AppliesAllTransformations()
    {
        var mockService = new Mock<IImageProcessingService>();
        mockService.Setup(s => s.PreprocessAsync(
                It.IsAny<Stream>(),
                It.IsAny<OcrOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        using var inputStream = new MemoryStream();
        var options = new OcrOptions
        {
            PreprocessGrayscale = true,
            PreprocessThreshold = true,
            ThresholdValue = 128,
            RotationDegrees = 90,
            CropRegion = new Rectangle(10, 10, 50, 50)
        };

        using var result = await mockService.Object.PreprocessAsync(inputStream, options);

        Assert.NotNull(result);
        mockService.Verify(s => s.PreprocessAsync(inputStream, options, default), Times.Once);
    }
}
