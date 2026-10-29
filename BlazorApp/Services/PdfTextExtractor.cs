using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Xobject;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Extractor for PDF documents using iText7
/// Extracts text page by page with page number information
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
        return extension?.Equals(".pdf", StringComparison.OrdinalIgnoreCase) == true;
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
                    // Extract document metadata
                    var documentInfo = pdfDoc.GetDocumentInfo();
                    if (!string.IsNullOrEmpty(documentInfo.GetTitle()))
                    {
                        textBuilder.AppendLine($"Title: {documentInfo.GetTitle()}");
                    }
                    if (!string.IsNullOrEmpty(documentInfo.GetAuthor()))
                    {
                        textBuilder.AppendLine($"Author: {documentInfo.GetAuthor()}");
                    }
                    var creationDate = documentInfo.GetMoreInfo("CreationDate");
                    if (!string.IsNullOrEmpty(creationDate))
                    {
                        textBuilder.AppendLine($"Created: {creationDate}");
                    }
                    textBuilder.AppendLine();

                    int pageCount = pdfDoc.GetNumberOfPages();
                    _logger.LogInformation("Processing PDF with {PageCount} pages: {FilePath}", 
                        pageCount, filePath);

                    for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                    {
                        var page = pdfDoc.GetPage(pageNum);
                        
                        // Extract text from page
                        var strategy = new SimpleTextExtractionStrategy();
                        string pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);

                        // Check if page has content (not just images)
                        if (!string.IsNullOrWhiteSpace(pageText) || HasContent(page))
                        {
                            textBuilder.AppendLine($"--- Page {pageNum} ---");
                            
                            if (!string.IsNullOrWhiteSpace(pageText))
                            {
                                textBuilder.AppendLine(pageText.Trim());
                            }
                            
                            textBuilder.AppendLine();
                        }
                    }
                }

                var extractedText = textBuilder.ToString().Trim();
                _logger.LogInformation("Successfully extracted {Length} characters from PDF: {FilePath}", 
                    extractedText.Length, filePath);
                
                return extractedText;
            }
            catch (iText.Kernel.Exceptions.PdfException ex)
            {
                _logger.LogError(ex, "Corrupted or invalid PDF document: {FilePath}", filePath);
                throw new InvalidOperationException($"The PDF document is corrupted or invalid: {filePath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF document: {FilePath}", filePath);
                throw;
            }
        });
    }

    private bool HasContent(iText.Kernel.Pdf.PdfPage page)
    {
        try
        {
            // The content stream bytes include "Do" draw operators for any XObjects
            // (including images), so this single check covers both text and image content.
            var contentBytes = page.GetContentBytes();
            return contentBytes != null && contentBytes.Length > 0;
        }
        catch
        {
            return true; // Assume has content if we can't check
        }
    }
}
