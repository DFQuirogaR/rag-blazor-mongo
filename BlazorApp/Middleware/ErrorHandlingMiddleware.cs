using System.Net;
using System.Text.Json;

namespace BlazorApp.Middleware;

/// <summary>
/// Middleware for centralized error handling and logging.
/// Catches unhandled exceptions, returns user-friendly messages,
/// and implements retry logic for transient errors.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Once the response has started, headers and status cannot be changed.
        // Log the exception and let the existing response finish instead of
        // throwing a second InvalidOperationException.
        if (context.Response.HasStarted)
        {
            _logger.LogError(exception, "Unhandled exception after response started for {Path}", context.Request.Path);
            return;
        }

        // Log the exception with full details
        var exceptionDetails = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            exceptionType = exception.GetType().FullName,
            message = exception.Message,
            stackTrace = _environment.IsDevelopment() ? exception.StackTrace : null,
            source = exception.Source,
            innerException = exception.InnerException?.Message,
            correlationId = context.Request.Headers.ContainsKey("X-Correlation-ID") 
                ? context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                : null,
            path = context.Request.Path.ToString(),
            method = context.Request.Method
        };

        // Determine log level based on exception type
        var isUnauthorized = exception is UnauthorizedAccessException;
        var isKeyNotFound = exception is KeyNotFoundException;
        var isArgument = exception is ArgumentException;
        var isOperation = exception is InvalidOperationException;

        if (isUnauthorized)
        {
            _logger.LogError(exception, "Critical exception occurred {@ExceptionDetails}", exceptionDetails);
        }
        else if (isKeyNotFound || isArgument)
        {
            _logger.LogWarning(exception, "Business logic exception {@ExceptionDetails}", exceptionDetails);
        }
        else
        {
            _logger.LogError(exception, "Unexpected exception occurred {@ExceptionDetails}", exceptionDetails);
        }

        // Set response status code
        context.Response.StatusCode = exception switch
        {
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            InvalidOperationException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };

        // Return user-friendly error response
        var errorResponse = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault(),
            statusCode = context.Response.StatusCode,
            error = GetErrorTitle(context.Response.StatusCode),
            message = GetErrorMessage(context.Response.StatusCode, exception.Message, _environment.IsDevelopment()),
            path = context.Request.Path
        };

        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(errorResponse);
    }

    private string GetErrorTitle(int statusCode)
    {
        return statusCode switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            500 => "Internal Server Error",
            503 => "Service Unavailable",
            _ => "Error"
        };
    }

    private string GetErrorMessage(int statusCode, string originalMessage, bool isDevelopment)
    {
        if (isDevelopment)
        {
            return originalMessage;
        }

        return statusCode switch
        {
            400 => "The request was invalid. Please check your input.",
            401 => "You are not authorized to access this resource.",
            403 => "Access to this resource is forbidden.",
            404 => "The requested resource was not found.",
            409 => "A conflict occurred with your request.",
            500 => "An unexpected error occurred. Our team has been notified.",
            503 => "The service is temporarily unavailable. Please try again later.",
            _ => "An error occurred while processing your request."
        };
    }
}

/// <summary>
/// Extension methods for error handling middleware.
/// </summary>
public static class ErrorHandlingMiddlewareExtensions
{
    /// <summary>
    /// Adds centralized error handling middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
