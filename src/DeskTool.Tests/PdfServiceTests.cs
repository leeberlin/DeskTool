using DeskTool.Core.Models;
using DeskTool.Core.Services;
using Moq;

namespace DeskTool.Tests;

/// <summary>
/// Unit tests for PDF service.
/// </summary>
public class PdfServiceTests
{
    [Fact]
    public void PageRange_Parse_ValidRange_ReturnsCorrectPages()
    {
        var pages = PageRange.Parse("1-3", 10).ToList();

        Assert.Equal(3, pages.Count);
        Assert.Equal(new[] { 1, 2, 3 }, pages);
    }

    [Fact]
    public void PageRange_Parse_SinglePage_ReturnsPage()
    {
        var pages = PageRange.Parse("5", 10).ToList();

        Assert.Single(pages);
        Assert.Equal(5, pages[0]);
    }

    [Fact]
    public void PageRange_Parse_MixedRanges_ReturnsAllPages()
    {
        var pages = PageRange.Parse("1-2, 5, 8-10", 10).ToList();

        Assert.Equal(6, pages.Count);
        Assert.Equal(new[] { 1, 2, 5, 8, 9, 10 }, pages);
    }

    [Fact]
    public void PageRange_Parse_OutOfRange_ClampsToBounds()
    {
        var pages = PageRange.Parse("0-15", 10).ToList();

        Assert.Equal(10, pages.Count);
        Assert.Equal(1, pages.First());
        Assert.Equal(10, pages.Last());
    }

    [Fact]
    public async Task MergeAsync_WithMultipleFiles_ReturnsSuccess()
    {
        var mockService = new Mock<IPdfService>();
        mockService.Setup(s => s.MergeAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<PdfOperationOptions>()))
            .ReturnsAsync(new PdfMergeResult("output.pdf", 10, 2, true));

        var result = await mockService.Object.MergeAsync(
            new[] { "file1.pdf", "file2.pdf" },
            "output.pdf");

        Assert.True(result.Success);
        Assert.Equal(2, result.SourceFileCount);
        Assert.Equal(10, result.TotalPages);
    }

    [Fact]
    public async Task SplitAsync_WithValidRange_ReturnsSuccess()
    {
        var mockService = new Mock<IPdfService>();
        mockService.Setup(s => s.SplitAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PdfOperationOptions>()))
            .ReturnsAsync(new PdfSplitResult(new[] { "output.pdf" }, 3, true));

        var result = await mockService.Object.SplitAsync(
            "input.pdf",
            "1-3",
            "output.pdf");

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalPagesExtracted);
    }

    [Fact]
    public async Task LoadAsync_ValidPdf_ReturnsPdfDocument()
    {
        var mockService = new Mock<IPdfService>();
        mockService.Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentModel
            {
                FilePath = "test.pdf",
                PageCount = 5,
                HasTextLayer = true,
                Pages = Enumerable.Range(1, 5)
                    .Select(i => new PdfPageModel { PageNumber = i })
                    .ToList()
            });

        var doc = await mockService.Object.LoadAsync("test.pdf");

        Assert.NotNull(doc);
        Assert.Equal(5, doc.PageCount);
        Assert.True(doc.HasTextLayer);
    }

    [Fact]
    public async Task ExtractTextAsync_PdfWithText_ReturnsText()
    {
        var mockService = new Mock<IPdfService>();
        var mockDoc = new PdfDocumentModel { PageCount = 1 };
        
        mockService.Setup(s => s.ExtractTextAsync(It.IsAny<PdfDocumentModel>(), 0))
            .ReturnsAsync("Hello World");

        var text = await mockService.Object.ExtractTextAsync(mockDoc, 0);

        Assert.Equal("Hello World", text);
    }

    [Fact]
    public async Task RotatePagesAsync_ValidPages_CompletesSuccessfully()
    {
        var mockService = new Mock<IPdfService>();
        var mockDoc = new PdfDocumentModel { PageCount = 5 };
        var pagesArray = new[] { 1, 2 };

        mockService.Setup(s => s.RotatePagesAsync(
                It.IsAny<PdfDocumentModel>(),
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        await mockService.Object.RotatePagesAsync(mockDoc, pagesArray, 90);

        mockService.Verify(s => s.RotatePagesAsync(
            It.Is<PdfDocumentModel>(d => d == mockDoc),
            It.Is<IEnumerable<int>>(e => e.SequenceEqual(pagesArray)),
            It.Is<int>(d => d == 90)), Times.Once);
    }
}
