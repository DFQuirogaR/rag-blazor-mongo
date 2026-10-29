using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Extractor for Word (.docx) documents using OpenXml
/// Extracts text while preserving document structure (headings, paragraphs, lists)
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
        return extension?.Equals(".docx", StringComparison.OrdinalIgnoreCase) == true;
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
                    // Extract document metadata
                    var coreProperties = doc.PackageProperties;
                    if (coreProperties != null)
                    {
                        if (!string.IsNullOrEmpty(coreProperties.Title))
                        {
                            textBuilder.AppendLine($"Title: {coreProperties.Title}");
                        }
                        if (!string.IsNullOrEmpty(coreProperties.Creator))
                        {
                            textBuilder.AppendLine($"Author: {coreProperties.Creator}");
                        }
                        if (coreProperties.Created.HasValue)
                        {
                            textBuilder.AppendLine($"Created: {coreProperties.Created.Value}");
                        }
                        textBuilder.AppendLine();
                    }

                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body == null)
                    {
                        _logger.LogWarning("Document body is empty: {FilePath}", filePath);
                        return string.Empty;
                    }

                    // Extract text with structure preservation
                    foreach (var element in body.Elements())
                    {
                        ExtractTextFromElement(element, textBuilder);
                    }
                }

                var extractedText = textBuilder.ToString().Trim();
                _logger.LogInformation("Successfully extracted {Length} characters from Word document: {FilePath}", 
                    extractedText.Length, filePath);
                
                return extractedText;
            }
            catch (OpenXmlPackageException ex)
            {
                _logger.LogError(ex, "Corrupted Word document: {FilePath}", filePath);
                throw new InvalidOperationException($"The Word document is corrupted or invalid: {filePath}", ex);
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
        // Handle different element types with structure preservation
        switch (element)
        {
            case Paragraph paragraph:
                var paragraphText = ProcessParagraph(paragraph);
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    textBuilder.AppendLine(paragraphText);
                }
                break;

            case Table table:
                ExtractTable(table, textBuilder);
                break;

            case SectionProperties sectionProperties:
                // Skip section properties as they contain page setup, not content
                break;

            default:
                // Recursively process child elements
                foreach (var child in element.Elements())
                {
                    ExtractTextFromElement(child, textBuilder);
                }
                break;
        }
    }

    private string ProcessParagraph(Paragraph paragraph)
    {
        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var textRuns = paragraph.Elements<Run>().Select(r => r.InnerText).Where(t => !string.IsNullOrEmpty(t));
        var paragraphText = string.Join(" ", textRuns).Trim();

        // Add visual indicators for different paragraph styles (headings)
        if (!string.IsNullOrEmpty(style))
        {
            if (style.Contains("Heading", StringComparison.OrdinalIgnoreCase))
            {
                var level = style.Replace("Heading", "").Replace("Char", "").Trim();
                var hashPrefix = new string('#', Math.Min(int.TryParse(level, out var l) ? l : 1, 6)) + " ";
                return $"{hashPrefix}{paragraphText}";
            }
        }

        return paragraphText;
    }

    private void ExtractTable(Table table, StringBuilder textBuilder)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0) return;

        // Extract table headers
        var headerRow = rows[0];
        var headers = headerRow.Elements<TableCell>()
            .Select(cell => cell.InnerText.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        // Extract data rows
        var dataRows = rows.Skip(1).Select(row => 
        {
            var cells = row.Elements<TableCell>()
                .Select(cell => cell.InnerText.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            return string.Join(" | ", cells);
        }).Where(rowText => !string.IsNullOrEmpty(rowText)).ToList();

        // Format as markdown-like table
        if (headers.Any() && dataRows.Any())
        {
            textBuilder.AppendLine("--- Table ---");
            textBuilder.AppendLine(string.Join(" | ", headers));
            textBuilder.AppendLine(new string('-', headers.Select(h => h.Length).Sum() + (headers.Count - 1) * 3));
            foreach (var row in dataRows)
            {
                textBuilder.AppendLine(row);
            }
            textBuilder.AppendLine();
        }
        else if (dataRows.Any())
        {
            // Plain table without headers
            textBuilder.AppendLine("--- Table ---");
            foreach (var row in dataRows)
            {
                textBuilder.AppendLine(row);
            }
            textBuilder.AppendLine();
        }
    }
}
