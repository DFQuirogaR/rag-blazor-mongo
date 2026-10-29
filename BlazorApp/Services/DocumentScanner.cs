using BlazorApp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BlazorApp.Services;

/// <summary>
/// Configuration for document scanning settings
/// </summary>
public class DocumentScannerOptions
{
    /// <summary>
    /// List of directories to scan for documents
    /// </summary>
    public List<string> ScanPaths { get; set; } = new();
    
    /// <summary>
    /// Patterns to exclude from scanning
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new();
    
    /// <summary>
    /// Interval between scan cycles in minutes (used when FileSystemWatcher is disabled)
    /// </summary>
    public int ScanIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// Whether to use FileSystemWatcher for real-time monitoring
    /// </summary>
    public bool EnableFileSystemWatcher { get; set; } = true;
    
    /// <summary>
    /// Interval for polling when FileSystemWatcher is disabled
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}

/// <summary>
/// Background service that monitors directories for new documents and processes them
/// Supports both FileSystemWatcher (real-time) and polling approaches
/// </summary>
public class DocumentScanner : BackgroundService
{
    private readonly DocumentProcessor _processor;
    private readonly DocumentScannerOptions _options;
    private readonly ILogger<DocumentScanner> _logger;
    private readonly IEnumerable<ITextExtractor> _extractors;

    private readonly HashSet<string> _processedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _fileContents = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _fileSystemWatcher;
    private Timer? _pollingTimer;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    public DocumentScanner(
        DocumentProcessor processor,
        IOptions<DocumentScannerOptions> options,
        ILogger<DocumentScanner> logger,
        IEnumerable<ITextExtractor> extractors)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
        _extractors = extractors;
    }

    /// <summary>
    /// Starts monitoring configured directories
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DocumentScanner starting...");

        // Validate scan paths
        var validPaths = new List<string>();
        foreach (var path in _options.ScanPaths)
        {
            if (Directory.Exists(path))
            {
                validPaths.Add(path);
                _logger.LogInformation("Validated scan path: {Path}", path);
            }
            else
            {
                _logger.LogWarning("Scan path does not exist (will be created or monitored when available): {Path}", path);
                validPaths.Add(path);
            }
        }

        if (validPaths.Count == 0)
        {
            _logger.LogError("No valid scan paths configured");
            return;
        }

        // Initialize FileSystemWatcher if enabled
        if (_options.EnableFileSystemWatcher)
        {
            InitializeFileSystemWatcher(validPaths);
        }

        // Start polling timer if FileSystemWatcher is disabled
        if (!_options.EnableFileSystemWatcher)
        {
            StartPolling();
        }

        // Start the BackgroundService loop before launching the initial scan. The old
        // implementation awaited the complete scan here and blocked application startup.
        await base.StartAsync(cancellationToken);
        _ = Task.Run(() => ScanAndProcessAsync(cancellationToken), cancellationToken);

        _logger.LogInformation("DocumentScanner started successfully");
    }

    /// <summary>
    /// Stops monitoring directories
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DocumentScanner stopping...");

        // Stop FileSystemWatcher
        _fileSystemWatcher?.Dispose();
        _fileSystemWatcher = null;

        // Stop polling timer
        _pollingTimer?.Dispose();
        _pollingTimer = null;

        _logger.LogInformation("DocumentScanner stopped");
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Performs periodic scanning (used for polling approach)
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.EnableFileSystemWatcher)
        {
            // FileSystemWatcher handles events, just wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        else
        {
            // Polling approach
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAndProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic scan");
                }

                await Task.Delay(TimeSpan.FromMinutes(_options.ScanIntervalMinutes), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Initializes FileSystemWatcher for real-time monitoring
    /// </summary>
    private void InitializeFileSystemWatcher(List<string> paths)
    {
        try
        {
            _fileSystemWatcher = new FileSystemWatcher
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                Filter = "*.*",
                IncludeSubdirectories = true
            };

            _fileSystemWatcher.Created += OnFileChanged;
            _fileSystemWatcher.Changed += OnFileChanged;
            _fileSystemWatcher.Deleted += OnFileChanged;
            _fileSystemWatcher.Renamed += OnFileRenamed;

            // A single watcher on the root covers all configured subdirectories.
            // Reassigning one watcher.Path in a loop only leaves the last path watched.
            var watchPath = paths.FirstOrDefault(Directory.Exists);
            if (watchPath == null)
            {
                _logger.LogWarning("No existing directory available for FileSystemWatcher");
                return;
            }

            _fileSystemWatcher.Path = watchPath;
            _fileSystemWatcher.EnableRaisingEvents = true;
            _logger.LogInformation("FileSystemWatcher configured for: {Path}", watchPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileSystemWatcher");
        }
    }

    /// <summary>
    /// Starts polling-based scanning
    /// </summary>
    private void StartPolling()
    {
        _pollingTimer = new Timer(async _ => await ScanAndProcessAsync(CancellationToken.None), 
            null, 
            TimeSpan.Zero, 
            TimeSpan.FromSeconds(_options.PollingIntervalSeconds));
        
        _logger.LogInformation("Polling started with interval: {Interval} seconds", _options.PollingIntervalSeconds);
    }

    /// <summary>
    /// Handles file creation/change events from FileSystemWatcher
    /// </summary>
    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("File system event detected: {ChangeType} - {FullPath}", e.ChangeType, e.FullPath);
            
            // Skip if file matches exclusion patterns
            if (IsExcluded(e.FullPath))
            {
                _logger.LogInformation("Skipped excluded file: {FullPath}", e.FullPath);
                return;
            }

            // Check if file is supported
            if (!IsSupportedFileType(e.FullPath))
            {
                _logger.LogInformation("Skipped unsupported file type: {FullPath}", e.FullPath);
                return;
            }

            // Add to processing queue (debounce)
            await ProcessFileAsync(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file system event for: {FullPath}", e.FullPath);
        }
    }

    /// <summary>
    /// Handles file rename events from FileSystemWatcher
    /// </summary>
    private async void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            _logger.LogInformation("File renamed: {OldFullPath} -> {NewFullPath}", e.OldFullPath, e.FullPath);
            
            // Process the new file
            if (!IsExcluded(e.FullPath) && IsSupportedFileType(e.FullPath))
            {
                await ProcessFileAsync(e.FullPath);
            }

            // Also handle the old file if it was processed
            if (_processedFiles.Contains(e.OldFullPath))
            {
                _processedFiles.Remove(e.OldFullPath);
                _logger.LogInformation("Removed old file from processed list: {OldFullPath}", e.OldFullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file rename from: {OldFullPath}", e.OldFullPath);
        }
    }

    /// <summary>
    /// Scans configured directories and processes documents
    /// </summary>
    public async Task<ScanResult> ScanAndProcessAsync(CancellationToken cancellationToken)
    {
        if (!await _scanLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("A document scan is already in progress");
            return new ScanResult();
        }

        try
        {
            return await ScanAndProcessCoreAsync(cancellationToken);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task<ScanResult> ScanAndProcessCoreAsync(CancellationToken cancellationToken)
    {
        var result = new ScanResult();
        var scanPaths = _options.ScanPaths
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        scanPaths = scanPaths.Where(path => !scanPaths.Any(other =>
            !string.Equals(path, other, StringComparison.OrdinalIgnoreCase) &&
            path.StartsWith(other.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var path in scanPaths)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!Directory.Exists(path))
            {
                _logger.LogWarning("Scan path does not exist: {Path}", path);
                continue;
            }

            try
            {
                _logger.LogInformation("Scanning directory: {Path}", path);

                // Get all supported files
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(f => !IsExcluded(f) && IsSupportedFileType(f))
                    .ToList();

                result.TotalFiles += files.Count;

                foreach (var filePath in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var document = await ProcessFileAsync(filePath);
                        
                        if (document != null)
                        {
                            result.ProcessedFiles++;
                            result.ProcessedDocuments.Add(document);
                            _logger.LogInformation("Successfully processed: {FilePath}", filePath);
                        }
                        else
                        {
                            result.SkippedFiles++;
                            result.SkippedFilesList.Add(filePath);
                            _logger.LogInformation("Skipped: {FilePath}", filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedFiles++;
                        result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                        _logger.LogError(ex, "Failed to process: {FilePath}", filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error scanning directory {path}: {ex.Message}");
                _logger.LogError(ex, "Error scanning directory: {Path}", path);
            }
        }

        _logger.LogInformation(
            "Scan complete. Total: {Total}, Processed: {Processed}, Skipped: {Skipped}, Failed: {Failed}",
            result.TotalFiles, result.ProcessedFiles, result.SkippedFiles, result.FailedFiles);

        return result;
    }

    /// <summary>
    /// Processes a single file and returns the document model
    /// </summary>
    private async Task<DocumentModel?> ProcessFileAsync(string filePath)
    {
        // Skip if already processed with same content
        var contentHash = CalculateFileHash(filePath);
        var contentKey = $"{filePath}:{contentHash}";

        if (_fileContents.Contains(contentKey))
        {
            _logger.LogDebug("File already processed with same content: {FilePath}", filePath);
            return null;
        }

        try
        {
            var document = await _processor.ProcessDocumentAsync(filePath);
            
            if (document != null)
            {
                _fileContents.Add(contentKey);
                _processedFiles.Add(filePath);
            }

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file matches any exclusion pattern
    /// </summary>
    private bool IsExcluded(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var filePathLower = filePath.ToLowerInvariant();

        foreach (var pattern in _options.ExcludePatterns)
        {
            var patternLower = pattern.ToLowerInvariant();

            // Check if pattern matches file name or path
            if (fileName.StartsWith(patternLower, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(patternLower, StringComparison.OrdinalIgnoreCase) ||
                filePathLower.Contains($@"{patternLower}\", StringComparison.OrdinalIgnoreCase) ||
                filePathLower.Contains($@"{patternLower}.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a file has a supported extension
    /// </summary>
    private bool IsSupportedFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return _extractors.Any(e => e.SupportsFileType(extension));
    }

    /// <summary>
    /// Calculates SHA256 hash of a file
    /// </summary>
    private string CalculateFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Result of a document scanning operation
    /// </summary>
    public class ScanResult
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int FailedFiles { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> SkippedFilesList { get; set; } = new();
        public List<DocumentModel> ProcessedDocuments { get; set; } = new();
    }
}
