namespace BlazorApp.Services;

/// <summary>
/// Interface for extracting text from documents
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text from the specified file
    /// </summary>
    Task<string> ExtractTextAsync(string filePath);
    
    /// <summary>
    /// Checks if the extractor supports the given file type
    /// </summary>
    bool SupportsFileType(string extension);
}
