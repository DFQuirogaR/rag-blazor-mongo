using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorApp.Logging;

/// <summary>
/// Extension methods for structured logging throughout the application.
/// Provides helper methods for common log patterns and logging scopes.
/// </summary>
public static class StructuredLogExtensions
{
    /// <summary>
    /// Logs a RAG operation with structured metadata.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="operation">Name of the RAG operation.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="query">Optional user query.</param>
    /// <param name="resultsCount">Number of results found.</param>
    /// <param name="additionalData">Additional structured data.</param>
    public static void LogRagOperation(
        this ILogger logger,
        string operation,
        long durationMs,
        bool success,
        string? query = null,
        int resultsCount = 0,
        params (string Key, object Value)[] additionalData)
    {
        var logData = new
        {
            operation,
            durationMs,
            success,
            query,
            resultsCount,
            timestamp = DateTime.UtcNow.ToString("o"),
            additionalData = additionalData.ToDictionary(d => d.Key, d => d.Value)
        };

        if (success)
        {
            logger.LogInformation("RAG operation completed {@LogData}", logData);
        }
        else
        {
            logger.LogError("RAG operation failed {@LogData}", logData);
        }
    }

    /// <summary>
    /// Logs a database operation with timing.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="operation">Name of the database operation.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="rowCount">Number of affected rows.</param>
    public static void LogDatabaseOperation(
        this ILogger logger,
        string operation,
        long durationMs,
        bool success,
        int rowCount = 0)
    {
        var logData = new
        {
            operation,
            durationMs,
            success,
            rowCount,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        if (durationMs > 500)
        {
            logger.LogWarning("Slow database operation detected {@LogData}", logData);
        }
        else if (success)
        {
            logger.LogInformation("Database operation completed {@LogData}", logData);
        }
        else
        {
            logger.LogError("Database operation failed {@LogData}", logData);
        }
    }

    /// <summary>
    /// Logs an LLM API call with structured data.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="model">Model name used.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="success">Whether the call succeeded.</param>
    /// <param name="tokensUsed">Number of tokens used.</param>
    /// <param name="promptTokens">Prompt token count.</param>
    /// <param name="completionTokens">Completion token count.</param>
    public static void LogLlmCall(
        this ILogger logger,
        string model,
        long durationMs,
        bool success,
        int tokensUsed = 0,
        int promptTokens = 0,
        int completionTokens = 0)
    {
        var logData = new
        {
            model,
            durationMs,
            success,
            tokensUsed,
            promptTokens,
            completionTokens,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        if (durationMs > 5000)
        {
            logger.LogWarning("Slow LLM call detected {@LogData}", logData);
        }
        else if (success)
        {
            logger.LogInformation("LLM call completed {@LogData}", logData);
        }
        else
        {
            logger.LogError("LLM call failed {@LogData}", logData);
        }
    }

    /// <summary>
    /// Logs document processing with structured metadata.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="filePath">Path to the document.</param>
    /// <param name="operation">Processing operation.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="success">Whether processing succeeded.</param>
    /// <param name="chunksCount">Number of chunks created.</param>
    /// <param name="fileSizeBytes">File size in bytes.</param>
    public static void LogDocumentProcessing(
        this ILogger logger,
        string filePath,
        string operation,
        long durationMs,
        bool success,
        int chunksCount = 0,
        long fileSizeBytes = 0)
    {
        var logData = new
        {
            filePath,
            operation,
            durationMs,
            success,
            chunksCount,
            fileSizeBytes,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        if (success)
        {
            logger.LogInformation("Document processing completed {@LogData}", logData);
        }
        else
        {
            logger.LogError("Document processing failed {@LogData}", logData);
        }
    }

    /// <summary>
    /// Creates a logging scope for grouped operations.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="correlationId">Correlation ID for the scope.</param>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="additionalData">Additional scoped data.</param>
    /// <returns>IDisposable scope.</returns>
    public static IDisposable BeginRagScope(
        this ILogger logger,
        string correlationId,
        string operationName,
        params (string Key, object Value)[] additionalData)
    {
        var scopeData = new
        {
            correlationId,
            operationName,
            timestamp = DateTime.UtcNow.ToString("o"),
            additionalData = additionalData.ToDictionary(d => d.Key, d => d.Value)
        };

        return logger.BeginScope(scopeData);
    }

    /// <summary>
    /// Logs a warning with structured data for performance concerns.
    /// </summary>
    public static void LogPerformanceWarning(
        this ILogger logger,
        string warningType,
        string message,
        long durationMs,
        params (string Key, object Value)[] additionalData)
    {
        var logData = new
        {
            warningType,
            message,
            durationMs,
            timestamp = DateTime.UtcNow.ToString("o"),
            additionalData = additionalData.ToDictionary(d => d.Key, d => d.Value)
        };

        logger.LogWarning("Performance warning: {Message} {@LogData}", message, logData);
    }

    /// <summary>
    /// Logs metrics snapshot for dashboard consumption.
    /// </summary>
    public static void LogMetricsSnapshot(
        this ILogger logger,
        string metricsSource,
        int totalRequests,
        double averageDurationMs,
        long p95DurationMs,
        long p99DurationMs,
        int errorCount)
    {
        var logData = new
        {
            metricsSource,
            totalRequests,
            averageDurationMs,
            p95DurationMs,
            p99DurationMs,
            errorCount,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        logger.LogInformation("Metrics snapshot {@LogData}", logData);
    }

    /// <summary>
    /// Logs embedding generation with structured data.
    /// </summary>
    public static void LogEmbeddingGeneration(
        this ILogger logger,
        string model,
        int dimensionCount,
        long durationMs,
        bool success,
        int chunkCount = 1)
    {
        var logData = new
        {
            model,
            dimensionCount,
            durationMs,
            success,
            chunkCount,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        logger.LogInformation("Embedding generation {@LogData}", logData);
    }
}

/// <summary>
/// Helper class for logging RAG operations with automatic scope management.
/// </summary>
public class RagLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly IDisposable? _scope;
    private readonly Stopwatch _stopwatch;
    private readonly string _operationName;
    private readonly string _correlationId;
    private bool _completed = false;

    public RagLogger(ILogger logger, string operationName, string correlationId)
    {
        _logger = logger;
        _operationName = operationName;
        _correlationId = correlationId;
        _stopwatch = Stopwatch.StartNew();

        _scope = logger.BeginScope(new
        {
            correlationId,
            operationName,
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>
    /// Marks the operation as successful and logs completion.
    /// </summary>
    public void Success(string? message = null)
    {
        _stopwatch.Stop();
        _completed = true;

        var logData = new
        {
            operation = _operationName,
            durationMs = _stopwatch.ElapsedMilliseconds,
            success = true,
            message,
            correlationId = _correlationId,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        _logger.LogInformation("Operation completed successfully {@LogData}", logData);
    }

    /// <summary>
    /// Marks the operation as failed and logs error.
    /// </summary>
    public void Failure(string? message = null, Exception? exception = null)
    {
        _stopwatch.Stop();
        _completed = true;

        var logData = new
        {
            operation = _operationName,
            durationMs = _stopwatch.ElapsedMilliseconds,
            success = false,
            message,
            correlationId = _correlationId,
            timestamp = DateTime.UtcNow.ToString("o"),
            exceptionType = exception?.GetType().FullName,
            exceptionMessage = exception?.Message
        };

        _logger.LogError("Operation failed {@LogData}", logData);
    }

    /// <summary>
    /// Logs progress with message.
    /// </summary>
    public void Progress(string message)
    {
        _logger.LogInformation("Operation progress: {Message} [Operation: {Operation}, Duration: {Duration}ms]",
            message, _operationName, _stopwatch.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            Failure("Operation not completed");
        }

        _scope?.Dispose();
    }
}
