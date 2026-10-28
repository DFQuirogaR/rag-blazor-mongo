using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Extractor for Word (.docx) documents using OpenXml
/// </summary>
public class WordTextExtractor : ITextExtractor
{
    private readonly ILogger<WordTextExtractor> _logger;

    public WordTextExtractor(ILogger<WordTextExtractor> logger)
    {
        _logger = logger;
    }

    public bool SupportsFileType(string extension)
    {
        return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
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

                using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body == null)
                    {
                        _logger.LogWarning("Document body is empty: {FilePath}", filePath);
                        return string.Empty;
                    }

                    foreach (var element in body.Elements())
                    {
                        ExtractTextFromElement(element, textBuilder);
                    }
                }

                var extractedText = textBuilder.ToString();
                _logger.LogInformation("Successfully extracted {Length} characters from {FilePath}", 
                    extractedText.Length, filePath);
                
                return extractedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from Word document: {FilePath}", filePath);
                throw;
            }
        });
    }

    private void ExtractTextFromElement(DocumentFormat.OpenXml.OpenXmlElement element, StringBuilder textBuilder)
    {
        if (element is Paragraph paragraph)
        {
            var paragraphText = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(paragraphText))
            {
                textBuilder.AppendLine(paragraphText);
            }
        }
        else if (element is Table table)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var rowTexts = new List<string>();
                foreach (var cell in row.Elements<TableCell>())
                {
                    var cellText = cell.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(cellText))
                    {
                        rowTexts.Add(cellText);
                    }
                }
                if (rowTexts.Any())
                {
                    textBuilder.AppendLine(string.Join(" | ", rowTexts));
                }
            }
            textBuilder.AppendLine();
        }
        else
        {
            // Recursively process child elements
            foreach (var child in element.Elements())
            {
                ExtractTextFromElement(child, textBuilder);
            }
        }
    }
}
