using System.Diagnostics;
using System.Text.Json;

namespace BlazorApp.Middleware;

/// <summary>
/// Middleware for structured request logging with correlation IDs.
/// Captures all incoming requests with timing, user info, and headers.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or reuse correlation ID
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? 
                           Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Start timing
        var stopwatch = Stopwatch.StartNew();

        // Extract user information
        var userInfo = GetUserInfo(context);

        // Log request start with structured data
        var requestLog = new
        {
            correlationId,
            timestamp = DateTime.UtcNow.ToString("o"),
            method = context.Request.Method,
            path = context.Request.Path,
            query = context.Request.QueryString.ToString(),
            userAgent = context.Request.Headers["User-Agent"].FirstOrDefault(),
            remoteIp = context.Connection.RemoteIpAddress?.ToString(),
            userId = userInfo.UserId,
            userName = userInfo.UserName,
            headers = GetSafeHeaders(context.Request.Headers)
        };

        if (_environment.IsDevelopment())
        {
            _logger.LogDebug("Request started {@RequestLog}", requestLog);
        }
        else
        {
            _logger.LogInformation("Request started {@RequestLog}", requestLog);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log request completion with timing
            var responseLog = new
            {
                correlationId,
                timestamp = DateTime.UtcNow.ToString("o"),
                statusCode = context.Response.StatusCode,
                durationMs = stopwatch.ElapsedMilliseconds,
                responseHeaders = GetSafeHeaders(context.Response.Headers),
                requestBodySize = context.Request.ContentLength ?? 0,
                // Kestrel's response stream is not seekable. Use the declared
                // Content-Length when available instead of reading Stream.Length.
                responseBodySize = context.Response.ContentLength ?? 0
            };

            if (context.Response.StatusCode >= 500)
            {
                _logger.LogError("Request failed {@ResponseLog}", responseLog);
            }
            else if (context.Response.StatusCode >= 400)
            {
                _logger.LogWarning("Request completed with warning {@ResponseLog}", responseLog);
            }
            else
            {
                _logger.LogInformation("Request completed {@ResponseLog}", responseLog);
            }
        }
    }

    private (string UserId, string UserName) GetUserInfo(HttpContext context)
    {
        // For Blazor Server with authentication
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return (context.User.FindFirst("sub")?.Value ?? context.User.Identity.Name ?? "authenticated",
                    context.User.Identity.Name ?? "unknown");
        }

        return ("anonymous", "anonymous");
    }

    private Dictionary<string, string[]> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string[]>();

        foreach (var header in headers)
        {
            // Exclude sensitive headers
            if (IsSensitiveHeader(header.Key))
            {
                safeHeaders[header.Key] = new[] { "***REDACTED***" };
            }
            else
            {
                safeHeaders[header.Key] = header.Value.ToArray();
            }
        }

        return safeHeaders;
    }

    private bool IsSensitiveHeader(string headerName)
    {
        var sensitiveHeaders = new[] 
        { 
            "Authorization", "Cookie", "Proxy-Authorization", 
            "X-API-Key", "X-Api-Key", "X-Auth-Token"
        };

        return sensitiveHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Extension methods for the request logging middleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds structured request logging middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
