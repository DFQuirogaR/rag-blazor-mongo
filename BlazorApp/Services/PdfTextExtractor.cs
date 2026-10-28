using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Extractor for PDF documents using iText7
/// </summary>
public class PdfTextExtractor : ITextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger;

    public PdfTextExtractor(ILogger<PdfTextExtractor> logger)
    {
        _logger = logger;
    }

    public bool SupportsFileType(string extension)
    {
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("File not found: {FilePath}", filePath);
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                var textBuilder = new StringBuilder();

                using (PdfReader reader = new PdfReader(filePath))
                using (PdfDocument pdfDoc = new PdfDocument(reader))
                {
                    int pageCount = pdfDoc.GetNumberOfPages();
                    _logger.LogInformation("Processing PDF with {PageCount} pages: {FilePath}", 
                        pageCount, filePath);

                    for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                    {
                        var page = pdfDoc.GetPage(pageNum);
                        var strategy = new SimpleTextExtractionStrategy();
                        string pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);

                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            textBuilder.AppendLine($"--- Página {pageNum} ---");
                            textBuilder.AppendLine(pageText.Trim());
                            textBuilder.AppendLine();
                        }
                    }
                }

                var extractedText = textBuilder.ToString();
                _logger.LogInformation("Successfully extracted {Length} characters from PDF: {FilePath}", 
                    extractedText.Length, filePath);
                
                return extractedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF document: {FilePath}", filePath);
                throw;
            }
        });
    }
}
