using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Scans configured directories for documents and processes them
/// </summary>
public class DocumentScanner
{
    private readonly DocumentProcessor _processor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentScanner> _logger;

    public DocumentScanner(
        DocumentProcessor processor,
        IConfiguration configuration,
        ILogger<DocumentScanner> logger)
    {
        _processor = processor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAndProcessAsync()
    {
        var result = new ScanResult();
        
        var documentPath = _configuration["DocumentSettings:ScanPath"] 
            ?? @"C:\Users\dquirogr\Documents\sharepoint_doc";

        if (!Directory.Exists(documentPath))
        {
            _logger.LogError("Document path does not exist: {Path}", documentPath);
            result.Errors.Add($"Ruta no encontrada: {documentPath}");
            return result;
        }

        _logger.LogInformation("Scanning documents from: {Path}", documentPath);

        // Supported file extensions
        var supportedExtensions = new[] { ".pdf", ".docx" };

        // Find all supported files
        var files = Directory.GetFiles(documentPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        _logger.LogInformation("Found {Count} documents to process", files.Count);
        result.TotalFiles = files.Count;

        foreach (var filePath in files)
        {
            try
            {
                _logger.LogInformation("Processing: {FilePath}", filePath);
                var document = await _processor.ProcessDocumentAsync(filePath);
                
                if (document != null)
                {
                    result.ProcessedFiles++;
                    result.ProcessedDocuments.Add(document);
                    _logger.LogInformation("Successfully processed: {FilePath}", filePath);
                }
                else
                {
                    result.SkippedFiles++;
                    _logger.LogWarning("Skipped: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                result.FailedFiles++;
                result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                _logger.LogError(ex, "Failed to process: {FilePath}", filePath);
            }
        }

        _logger.LogInformation(
            "Scan complete. Total: {Total}, Processed: {Processed}, Skipped: {Skipped}, Failed: {Failed}",
            result.TotalFiles, result.ProcessedFiles, result.SkippedFiles, result.FailedFiles);

        return result;
    }

    public class ScanResult
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int FailedFiles { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<DocumentModel> ProcessedDocuments { get; set; } = new();
    }
}
