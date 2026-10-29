using BlazorApp.Models;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Handles intelligent chunking of document content with overlap
/// Preserves semantic boundaries like paragraphs and sentences
/// </summary>
public class DocumentChunker
{
    private readonly int _chunkSize;
    private readonly int _overlapSize;
    private readonly ILogger<DocumentChunker> _logger;

    /// <summary>
    /// Creates a new DocumentChunker instance
    /// </summary>
    /// <param name="chunkSize">Target size for each chunk in characters (default: 1000)</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks (default: 200)</param>
    /// <param name="logger">Logger instance</param>
    public DocumentChunker(
        int chunkSize = 1000,
        int overlapSize = 200,
        ILogger<DocumentChunker> logger = null!)
    {
        _chunkSize = chunkSize;
        _overlapSize = overlapSize;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Splits document content into semantically meaningful chunks
    /// </summary>
    /// <param name="content">The document content to chunk</param>
    /// <param name="documentId">The ID of the parent document</param>
    /// <param name="chunkSize">Override default chunk size</param>
    /// <param name="overlapSize">Override default overlap size</param>
    /// <returns>List of chunk models with metadata</returns>
    public List<ChunkModel> ChunkDocument(
        string content,
        string documentId,
        int? chunkSize = null,
        int? overlapSize = null)
    {
        var actualChunkSize = chunkSize ?? _chunkSize;
        var actualOverlapSize = overlapSize ?? _overlapSize;

        _logger.LogInformation("Chunking document with size {DocSize} into chunks of {ChunkSize} with {OverlapSize} overlap",
            content.Length, actualChunkSize, actualOverlapSize);

        var chunks = new List<ChunkModel>();
        
        // Split content into paragraphs first to preserve semantic boundaries
        var paragraphs = SplitIntoParagraphs(content);
        
        var currentChunk = new StringBuilder();
        var currentChunkStartPos = 0;
        var chunkIndex = 0;
        var chunkMetadata = new ChunkMetadata();

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var paragraphPos = paragraph.Item2; // Start position in original document
            var paragraphText = paragraph.Item1;

            // Try to extract heading information
            var isHeading = IsHeading(paragraphText);
            var headingLevel = GetHeadingLevel(paragraphText);
            var sectionInfo = ExtractSectionInfo(paragraphText, paragraphs, i);

            if (currentChunk.Length + paragraphText.Length > actualChunkSize && currentChunk.Length > 0)
            {
                // Save current chunk
                var chunkText = currentChunk.ToString().Trim();
                
                chunks.Add(ChunkModel.Create(
                    documentId: documentId,
                    chunkIndex: chunkIndex,
                    content: chunkText,
                    startChar: currentChunkStartPos,
                    endChar: currentChunkStartPos + chunkText.Length,
                    metadata: chunkMetadata,
                    pageNumber: chunkMetadata.PageNumber,
                    chunkType: chunkMetadata.ContentType ?? "paragraph"
                ));

                _logger.LogDebug("Created chunk {ChunkIndex} with {Length} characters", chunkIndex, chunkText.Length);
                chunkIndex++;

                // Create overlap with previous content
                currentChunk.Clear();
                var overlapContent = GetOverlapContent(chunks.Last(), actualOverlapSize);
                currentChunk.Append(overlapContent);
                currentChunkStartPos = chunks.Last().StartChar + (chunks.Last().ChunkText.Length - overlapContent.Length);
                
                // Reset metadata for new chunk
                chunkMetadata = new ChunkMetadata();
            }

            // Add paragraph to current chunk
            if (currentChunk.Length > 0)
            {
                currentChunk.Append("\n\n");
            }
            currentChunk.Append(paragraphText);

            // Update chunk metadata if this is a heading
            if (isHeading)
            {
                chunkMetadata.HeadingLevel = headingLevel;
                chunkMetadata.ContentType = "heading";
                chunkMetadata.SectionTitle = ExtractSectionTitle(paragraphText);
            }
            else if (chunkMetadata.ContentType == null)
            {
                chunkMetadata.ContentType = "text";
            }

            // Update page number if present
            if (paragraphText.StartsWith("--- Page ") && paragraphText.EndsWith(" ---"))
            {
                var pageNumStr = paragraphText.Replace("--- Page ", "").Replace(" ---", "").Trim();
                if (int.TryParse(pageNumStr, out var pageNum))
                {
                    chunkMetadata.PageNumber = pageNum;
                }
            }
        }

        // Add remaining content as final chunk
        if (currentChunk.Length > 0)
        {
            var finalChunkText = currentChunk.ToString().Trim();
            
            chunks.Add(ChunkModel.Create(
                documentId: documentId,
                chunkIndex: chunkIndex,
                content: finalChunkText,
                startChar: currentChunkStartPos,
                endChar: currentChunkStartPos + finalChunkText.Length,
                metadata: chunkMetadata,
                pageNumber: chunkMetadata.PageNumber,
                chunkType: chunkMetadata.ContentType ?? "paragraph"
            ));

            _logger.LogDebug("Created final chunk {ChunkIndex} with {Length} characters", chunkIndex, finalChunkText.Length);
        }

        _logger.LogInformation("Created {ChunkCount} chunks from document", chunks.Count);
        return chunks;
    }

    /// <summary>
    /// Splits content into paragraphs, tracking original positions
    /// </summary>
    private List<(string Text, int StartPosition)> SplitIntoParagraphs(string content)
    {
        var paragraphs = new List<(string, int)>();
        var position = 0;

        // Split on double newlines (paragraph breaks)
        var splitChars = new[] { "\n\n", "\r\n\r\n" };
        var parts = content.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                // Find position in original content
                var pos = content.IndexOf(trimmed, position, StringComparison.Ordinal);
                if (pos >= 0)
                {
                    paragraphs.Add((trimmed, pos));
                    position = pos + trimmed.Length;
                }
            }
        }

        return paragraphs;
    }

    /// <summary>
    /// Determines if a text is a heading
    /// </summary>
    private bool IsHeading(string text)
    {
        // Check for markdown-style headings
        if (text.StartsWith("#") || text.StartsWith("##") || text.StartsWith("###"))
            return true;

        // Check for numbered sections (e.g., "1. Introduction", "3.2.1 Configuration")
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+(\.\d+)*\s+"))
            return true;

        // Check for all-caps short text (often headings)
        var words = text.Split(' ');
        if (words.Length <= 8 && words.All(w => w.Length > 0 && char.IsUpper(w[0])))
            return true;

        return false;
    }

    /// <summary>
    /// Extracts heading level from markdown-style heading
    /// </summary>
    private int? GetHeadingLevel(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^(#{1,6})\s+");
        if (match.Success)
        {
            return match.Groups[1].Length;
        }
        return null;
    }

    /// <summary>
    /// Extracts section information from paragraph
    /// </summary>
    private (string? Title, string? Number) ExtractSectionInfo(string text, List<(string, int)> paragraphs, int index)
    {
        // Try to extract section number
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^(\d+(\.\d+)*)\s+");
        if (match.Success)
        {
            var number = match.Groups[1].Value;
            var title = text.Substring(match.Length).Trim();
            return (title, number);
        }

        return (null, null);
    }

    /// <summary>
    /// Extracts section title from heading text
    /// </summary>
    private string? ExtractSectionTitle(string text)
    {
        // Remove markdown heading markers
        var clean = System.Text.RegularExpressions.Regex.Replace(text, @"^#{1,6}\s*", "");
        // Remove section numbers
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"^\d+(\.\d+)*\s*", "");
        return clean.Trim();
    }

    /// <summary>
    /// Gets overlap content from previous chunk
    /// </summary>
    private string GetOverlapContent(ChunkModel chunk, int overlapSize)
    {
        if (chunk.ChunkText.Length <= overlapSize)
            return chunk.ChunkText;

        return chunk.ChunkText.Substring(chunk.ChunkText.Length - overlapSize);
    }

    /// <summary>
    /// Gets approximate token count for text (rough estimate: 1 token ≈ 4 characters)
    /// </summary>
    public int GetTokenCount(string text)
    {
        return text.Length / 4;
    }
}
