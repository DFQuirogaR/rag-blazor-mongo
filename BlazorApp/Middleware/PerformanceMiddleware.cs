using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlazorApp.Middleware;

/// <summary>
/// Middleware for tracking request performance metrics.
/// Detects slow requests and tracks component-specific timing.
/// </summary>
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IMetricsCollector _metricsCollector;

    public PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger, IMetricsCollector metricsCollector)
    {
        _next = next;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var routeName = (endpoint as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern?.RawText ?? context.Request.Path.ToString();

        // Start timing
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Collect metrics
            _metricsCollector.RecordRequestDuration(routeName, elapsedMs, context.Response.StatusCode);

            // Log slow requests (> 2 seconds)
            if (elapsedMs > 2000)
            {
                _logger.LogWarning("Slow request detected - Route: {Route}, Duration: {Duration}ms, StatusCode: {StatusCode}", 
                    routeName, elapsedMs, context.Response.StatusCode);

                // Log detailed timing if available
                LogDetailedTiming(context, elapsedMs);
            }

            // Log metrics summary periodically
            LogMetricsSummary(elapsedMs, routeName, context.Response.StatusCode);
        }
    }

    private void LogDetailedTiming(HttpContext context, long elapsedMs)
    {
        var timingData = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            path = context.Request.Path,
            method = context.Request.Method,
            durationMs = elapsedMs,
            slowThresholdMs = 2000,
            isSlowRequest = elapsedMs > 2000,
            connectionId = context.Connection.Id,
            requestId = context.TraceIdentifier,
            correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        };

        _logger.LogWarning("Slow request details {@TimingData}", timingData);
    }

    private void LogMetricsSummary(long elapsedMs, string routeName, int statusCode)
    {
        // Only log periodically to avoid noise
        if (elapsedMs > 1000 || statusCode >= 500)
        {
            var summary = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                route = routeName,
                durationMs = elapsedMs,
                statusCode = statusCode,
                category = GetCategory(elapsedMs),
                isCritical = statusCode >= 500
            };

            if (statusCode >= 500)
            {
                _logger.LogError("Performance alert {@Summary}", summary);
            }
            else if (elapsedMs > 1000)
            {
                _logger.LogInformation("Performance metric {@Summary}", summary);
            }
        }
    }

    private string GetCategory(long elapsedMs)
    {
        if (elapsedMs < 100) return "fast";
        if (elapsedMs < 500) return "normal";
        if (elapsedMs < 2000) return "slow";
        return "very-slow";
    }
}

/// <summary>
/// Interface for metrics collection.
/// </summary>
public interface IMetricsCollector
{
    void RecordRequestDuration(string route, long durationMs, int statusCode);
    void RecordDatabaseQueryDuration(string operation, long durationMs);
    void RecordLlmCallDuration(long durationMs, bool isSuccess);
    void RecordRagOperation(string operation, long durationMs);
    MetricsSnapshot GetSnapshot();
}

/// <summary>
/// In-memory metrics collector implementation.
/// Collects and summarizes performance metrics for dashboard.
/// </summary>
public class MetricsCollector : IMetricsCollector
{
    private readonly object _lock = new();
    private readonly List<MetricEntry> _requestMetrics = new();
    private readonly List<MetricEntry> _databaseMetrics = new();
    private readonly List<MetricEntry> _llmMetrics = new();
    private readonly List<MetricEntry> _ragMetrics = new();

    public void RecordRequestDuration(string route, long durationMs, int statusCode)
    {
        lock (_lock)
        {
            _requestMetrics.Add(new MetricEntry
            {
                Timestamp = DateTime.UtcNow,
                Route = route,
                DurationMs = durationMs,
                StatusCode = statusCode
            });

            // Keep only last 1000 entries
            if (_requestMetrics.Count > 1000)
            {
                _requestMetrics.RemoveAt(0);
            }
        }
    }

    public void RecordDatabaseQueryDuration(string operation, long durationMs)
    {
        lock (_lock)
        {
            _databaseMetrics.Add(new MetricEntry
            {
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                DurationMs = durationMs
            });

            if (_databaseMetrics.Count > 1000)
            {
                _databaseMetrics.RemoveAt(0);
            }
        }
    }

    public void RecordLlmCallDuration(long durationMs, bool isSuccess)
    {
        lock (_lock)
        {
            _llmMetrics.Add(new MetricEntry
            {
                Timestamp = DateTime.UtcNow,
                DurationMs = durationMs,
                IsSuccess = isSuccess
            });

            if (_llmMetrics.Count > 1000)
            {
                _llmMetrics.RemoveAt(0);
            }
        }
    }

    public void RecordRagOperation(string operation, long durationMs)
    {
        lock (_lock)
        {
            _ragMetrics.Add(new MetricEntry
            {
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                DurationMs = durationMs
            });

            if (_ragMetrics.Count > 1000)
            {
                _ragMetrics.RemoveAt(0);
            }
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new MetricsSnapshot
            {
                RequestMetrics = GetSummary(_requestMetrics),
                DatabaseMetrics = GetSummary(_databaseMetrics),
                LlmMetrics = GetSummary(_llmMetrics),
                RagMetrics = GetSummary(_ragMetrics),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private MetricSummary GetSummary(List<MetricEntry> metrics)
    {
        if (metrics.Count == 0)
        {
            return new MetricSummary { Count = 0 };
        }

        var durations = metrics.Select(m => m.DurationMs).ToArray();
        var average = durations.Average();
        var min = durations.Min();
        var max = durations.Max();
        var standardDeviation = Math.Sqrt(durations.Select(d => Math.Pow(d - average, 2)).Average());

        return new MetricSummary
        {
            Count = metrics.Count,
            AverageMs = Math.Round(average, 2),
            MinMs = min,
            MaxMs = max,
            StandardDeviationMs = Math.Round(standardDeviation, 2),
            ThresholdBreaches = metrics.Count(m => m.DurationMs > 2000)
        };
    }
}

/// <summary>
/// Single metric entry.
/// </summary>
public class MetricEntry
{
    public DateTime Timestamp { get; set; }
    public string? Route { get; set; }
    public string? Operation { get; set; }
    public long DurationMs { get; set; }
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
}

/// <summary>
/// Summary statistics for metrics.
/// </summary>
public class MetricSummary
{
    public int Count { get; set; }
    public double AverageMs { get; set; }
    public long MinMs { get; set; }
    public long MaxMs { get; set; }
    public double StandardDeviationMs { get; set; }
    public int ThresholdBreaches { get; set; }
}

/// <summary>
/// Complete metrics snapshot for dashboard.
/// </summary>
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public MetricSummary RequestMetrics { get; set; } = new();
    public MetricSummary DatabaseMetrics { get; set; } = new();
    public MetricSummary LlmMetrics { get; set; } = new();
    public MetricSummary RagMetrics { get; set; } = new();
}

/// <summary>
/// Extension methods for performance middleware.
/// </summary>
public static class PerformanceMiddlewareExtensions
{
    /// <summary>
    /// Adds performance monitoring middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMiddleware>();
    }

    /// <summary>
    /// Registers the metrics collector service.
    /// </summary>
    public static IServiceCollection AddMetricsCollector(this IServiceCollection services)
    {
        services.AddSingleton<IMetricsCollector, MetricsCollector>();
        return services;
    }
}
