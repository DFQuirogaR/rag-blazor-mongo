namespace BlazorApp.Services;

/// <summary>
/// Interface for extracting text from documents
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text from the specified file
    /// </summary>
    /// <param name="filePath">Path to the document file</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(string filePath);
    
    /// <summary>
    /// Checks if the extractor supports the given file extension
    /// </summary>
    /// <param name="extension">File extension (e.g., ".docx", ".pdf")</param>
    /// <returns>True if supported, false otherwise</returns>
    bool SupportsFileType(string extension);
}
