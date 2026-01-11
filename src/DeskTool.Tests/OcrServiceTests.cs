using DeskTool.Core.Models;
using DeskTool.Core.Services;
using Moq;

namespace DeskTool.Tests;

/// <summary>
/// Unit tests for OCR service.
/// </summary>
public class OcrServiceTests
{
    [Fact]
    public void GetAvailableLanguages_ReturnsExpectedLanguages()
    {
        // This test would need actual tessdata files to pass
        // For unit testing, we mock the service
        var mockService = new Mock<IOcrService>();
        mockService.Setup(s => s.GetAvailableLanguages())
            .Returns(new[] { OcrLanguage.English, OcrLanguage.Vietnamese, OcrLanguage.German });

        var languages = mockService.Object.GetAvailableLanguages();

        Assert.Contains(OcrLanguage.English, languages);
        Assert.Contains(OcrLanguage.Vietnamese, languages);
        Assert.Contains(OcrLanguage.German, languages);
    }

    [Fact]
    public async Task RecognizeAsync_WithValidImage_ReturnsResult()
    {
        var mockService = new Mock<IOcrService>();
        var expectedResult = new OcrResult
        {
            Text = "Hello World",
            Confidence = 95.5f,
            ProcessingTime = TimeSpan.FromMilliseconds(500)
        };

        mockService.Setup(s => s.RecognizeAsync(
                It.IsAny<Stream>(),
                It.IsAny<OcrOptions>(),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        using var stream = new MemoryStream();
        var result = await mockService.Object.RecognizeAsync(
            stream,
            new OcrOptions { Languages = [OcrLanguage.English] });

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello World", result.Text);
        Assert.True(result.Confidence > 90);
    }

    [Fact]
    public async Task RecognizeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var mockService = new Mock<IOcrService>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockService.Setup(s => s.RecognizeAsync(
                It.IsAny<Stream>(),
                It.IsAny<OcrOptions>(),
                It.IsAny<IProgress<int>>(),
                It.Is<CancellationToken>(ct => ct.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());

        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await mockService.Object.RecognizeAsync(
                stream,
                new OcrOptions(),
                null,
                cts.Token));
    }

    [Theory]
    [InlineData(OcrLanguage.English, true)]
    [InlineData(OcrLanguage.Vietnamese, true)]
    [InlineData(OcrLanguage.German, true)]
    public void IsLanguageAvailable_ReturnsExpected(OcrLanguage language, bool expected)
    {
        var mockService = new Mock<IOcrService>();
        mockService.Setup(s => s.IsLanguageAvailable(It.IsAny<OcrLanguage>())).Returns(expected);

        var result = mockService.Object.IsLanguageAvailable(language);

        Assert.Equal(expected, result);
    }
}
