using BlazorApp.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorAppTests;

public class TextExtractorTests : IDisposable
{
    private readonly ILogger<WordTextExtractor> _wordLogger;
    private readonly ILogger<PdfTextExtractor> _pdfLogger;
    private readonly string _testDataPath;
    
    public TextExtractorTests()
    {
        _wordLogger = new Logger<WordTextExtractor>(new LoggerConfiguration().CreateLogger());
        _pdfLogger = new Logger<PdfTextExtractor>(new LoggerConfiguration().CreateLogger());
        
        // Setup test data directory
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
        Directory.CreateDirectory(_testDataPath);
    }
    
    [Fact]
    public async Task WordTextExtractor_SupportsExtension_ShouldReturnTrueForDocx()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        
        // Act
        var result = extractor.SupportsExtension(".docx");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task WordTextExtractor_SupportsExtension_ShouldReturnFalseForInvalidExtension()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        
        // Act
        var result = extractor.SupportsExtension(".txt");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task WordTextExtractor_SupportsExtension_ShouldHandleNullExtension()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        
        // Act
        var result = extractor.SupportsExtension(null);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task WordTextExtractor_SupportsExtension_ShouldHandleEmptyExtension()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        
        // Act
        var result = extractor.SupportsExtension(string.Empty);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task WordTextExtractor_SupportsExtension_ShouldBeCaseInsensitive()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        
        // Act
        var result = extractor.SupportsExtension(".DOCX");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task PdfTextExtractor_SupportsExtension_ShouldReturnTrueForPdf()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        
        // Act
        var result = extractor.SupportsExtension(".pdf");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task PdfTextExtractor_SupportsExtension_ShouldReturnFalseForInvalidExtension()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        
        // Act
        var result = extractor.SupportsExtension(".docx");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PdfTextExtractor_SupportsExtension_ShouldHandleNullExtension()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        
        // Act
        var result = extractor.SupportsExtension(null);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PdfTextExtractor_SupportsExtension_ShouldHandleEmptyExtension()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        
        // Act
        var result = extractor.SupportsExtension(string.Empty);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PdfTextExtractor_SupportsExtension_ShouldBeCaseInsensitive()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        
        // Act
        var result = extractor.SupportsExtension(".PDF");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task WordTextExtractor_ExtractText_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        string nonExistentFile = Path.Combine(_testDataPath, "nonexistent.docx");
        
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => extractor.ExtractTextAsync(nonExistentFile));
    }
    
    [Fact]
    public async Task PdfTextExtractor_ExtractText_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        string nonExistentFile = Path.Combine(_testDataPath, "nonexistent.pdf");
        
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => extractor.ExtractTextAsync(nonExistentFile));
    }
    
    [Fact]
    public async Task WordTextExtractor_ExtractText_ShouldExtractTextFromValidFile()
    {
        // Arrange
        var extractor = new WordTextExtractor(_wordLogger);
        string testFilePath = Path.Combine(_testDataPath, "test-document.docx");
        
        // Create a simple test document if it doesn't exist
        if (!File.Exists(testFilePath))
        {
            CreateTestWordDocument(testFilePath);
        }
        
        // Act
        var result = await extractor.ExtractTextAsync(testFilePath);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Test", result); // Should contain test content
    }
    
    [Fact]
    public async Task PdfTextExtractor_ExtractText_ShouldThrowInvalidOperationExceptionForCorruptedFile()
    {
        // Arrange
        var extractor = new PdfTextExtractor(_pdfLogger);
        string corruptedFilePath = Path.Combine(_testDataPath, "corrupted.pdf");
        
        // Create a corrupted PDF file
        File.WriteAllText(corruptedFilePath, "This is not a valid PDF file");
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => extractor.ExtractTextAsync(corruptedFilePath));
        Assert.Contains("corrupted", exception.Message.ToLowerInvariant());
    }
    
    private void CreateTestWordDocument(string filePath)
    {
        using (WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            
            Body body = mainPart.Document.Body;
            
            // Add a title
            AddHeading(body, "Test Document Title", 1);
            
            // Add a section with heading 2
            AddHeading(body, "Introduction", 2);
            AddParagraph(body, "This is a test paragraph to verify text extraction functionality.");
            
            // Add a list
            AddHeading(body, "Test List", 2);
            AddListParagraph(body, "First item in list");
            AddListParagraph(body, "Second item in list");
            AddListParagraph(body, "Third item in list");
            
            // Add a table
            AddHeading(body, "Sample Table", 2);
            AddTable(body);
            
            // Add more content
            AddHeading(body, "Conclusion", 2);
            AddParagraph(body, "This is the conclusion paragraph.");
            
            mainPart.Document.Save();
        }
    }
    
    private void AddHeading(Body body, string text, int level)
    {
        var paragraph = new Paragraph();
        var paragraphProperties = new ParagraphProperties();
        var paragraphStyleId = new ParagraphStyleId() { Val = $"Heading{level}" };
        paragraphProperties.Append(paragraphStyleId);
        
        var run = new Run();
        var textElement = new Text(text);
        run.Append(textElement);
        paragraph.Append(paragraphProperties);
        paragraph.Append(run);
        body.Append(paragraph);
    }
    
    private void AddParagraph(Body body, string text)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        var textElement = new Text(text);
        run.Append(textElement);
        paragraph.Append(run);
        body.Append(paragraph);
    }
    
    private void AddListParagraph(Body body, string text)
    {
        var paragraph = new Paragraph();
        var paragraphProperties = new ParagraphProperties();
        var numPr = new NumberingProperties();
        var numberingId = new NumberingId() { Val = 1 };
        numPr.Append(numberingId);
        paragraphProperties.Append(numPr);
        
        var run = new Run();
        var textElement = new Text(text);
        run.Append(textElement);
        
        paragraph.Append(paragraphProperties);
        paragraph.Append(run);
        body.Append(paragraph);
    }
    
    private void AddTable(Body body)
    {
        var table = new Table();
        var tableProperties = new TableProperties(
            new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }
        );
        table.Append(tableProperties);
        
        var tableRow1 = new TableRow();
        var tableCell1 = new TableCell(new Paragraph(new Run(new Text("Column 1"))));
        var tableCell2 = new TableCell(new Paragraph(new Run(new Text("Column 2"))));
        tableRow1.Append(tableCell1, tableCell2);
        
        var tableRow2 = new TableRow();
        var tableCell3 = new TableCell(new Paragraph(new Run(new Text("Value 1"))));
        var tableCell4 = new TableCell(new Paragraph(new Run(new Text("Value 2"))));
        tableRow2.Append(tableCell3, tableCell4);
        
        table.Append(tableRow1, tableRow2);
        body.Append(table);
    }
    
    public void Dispose()
    {
        // Cleanup test files
        try
        {
            if (Directory.Exists(_testDataPath))
            {
                Directory.Delete(_testDataPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
