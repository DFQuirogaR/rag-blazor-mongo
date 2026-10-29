# Logging and Monitoring Implementation - Summary

## Overview
Comprehensive logging and monitoring system has been implemented for the RAG Blazor application.

## Files Created

### Middleware Files

1. **Middleware/RequestLoggingMiddleware.cs**
   - Structured request logging with correlation IDs
   - Captures incoming request details (method, path, headers, user info)
   - Logs request/response timing
   - Excludes sensitive headers (Authorization, Cookie, etc.)
   - Logs at appropriate levels (Debug, Info, Warning, Error)

2. **Middleware/PerformanceMiddleware.cs**
   - Tracks request duration for all endpoints
   - Detects slow requests (> 2 seconds threshold)
   - Records metrics to in-memory collector
   - Provides summary statistics for dashboard

3. **Middleware/ErrorHandlingMiddleware.cs**
   - Centralized exception handling
   - Returns user-friendly error messages
   - Logs full exception details with correlation ID
   - Maps exception types to HTTP status codes

### Logging Files

4. **Logging/StructuredLogExtensions.cs**
   - Extension methods for structured logging
   - Helpers for common patterns (RAG operations, database operations, LLM calls, document processing)
   - Logging scopes for grouped operations
   - RagLogger class for scoped logging with automatic timing

5. **Program.cs** (Updated)
   - Configured Serilog for structured logging
   - Added request logging middleware
   - Added performance monitoring middleware
   - Added error handling middleware
   - Registered metrics collector service

6. **Components/Pages/Statistics.razor** (New)
   - Statistics dashboard component
   - Displays request, LLM, database, and RAG operation metrics
   - Real-time refresh capability

7. **Components/Pages/Statistics.razor.css** (New)
   - Modern dashboard styling with gradients
   - Responsive card layout

### Configuration Files

8. **BlazorApp.csproj** (Updated)
   - Added Serilog.AspNetCore package
   - Added Serilog.Sinks.Console package
   - Added Serilog.Sinks.File package

9. **appsettings.json** (Updated)
   - Added Serilog configuration
   - Configured console and file sinks
   - Set appropriate log levels

## Features Implemented

### 1. Structured Logging
- JSON-formatted log output
- Correlation IDs for request tracing
- Structured data in all log entries
- Serilog integration for rich logging

### 2. Request Logging
- Every request logged with full details
- Request timing captured
- User information extracted (authenticated/anonymous)
- Sensitive headers redacted

### 3. Performance Monitoring
- Request duration tracking
- Slow request detection (>2s threshold)
- Metrics collection for dashboard
- In-memory metrics storage

### 4. Error Handling
- Centralized exception handling
- User-friendly error messages
- Detailed error logging
- Proper HTTP status codes

### 5. Statistics Dashboard
- Real-time metrics visualization
- Request performance data
- LLM performance data
- Database query performance
- RAG operation metrics

## Architecture

```
┌─────────────────────────────────────────────┐
│          Request Flow                       │
├─────────────────────────────────────────────┤
│  [Client] → [ErrorHandling] → [RequestLog] │
│           → [Performance] → [Application]   │
│                                             │
│  [Application] → [MetricsCollector]         │
│  [MetricsCollector] → [Dashboard]           │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│          Log Flow                           │
├─────────────────────────────────────────────┤
│  [Application] → [Serilog] → [Console]      │
│                       → [File (logs/)]      │
└─────────────────────────────────────────────┘
```

## Usage Examples

### Logging in Services
```csharp
// Using extension methods
_logger.LogRagOperation("QueryDocuments", durationMs, success, query, resultsCount);
_logger.LogDatabaseOperation("FindChunks", durationMs, success, rowCount);
_logger.LogLlmCall(ollamaModel, durationMs, success, tokensUsed);

// Using RagLogger scope
using var ragLogger = new RagLogger(_logger, "ProcessQuery", correlationId);
ragLogger.Progress("Starting query processing");
ragLogger.Success("Query completed successfully");
```

### Configuring Middleware
```csharp
app.UseErrorHandling();
app.UseRequestLogging();
app.UsePerformanceMonitoring();
```

## Configuration

### Log Levels
- Development: Debug
- Production: Information

### Log Files
- Location: `logs/rag-blazor-.log`
- Rolling: Daily
- Retention: 30 days

### Performance Thresholds
- Slow Request: > 2000ms
- Slow Database: > 500ms
- Slow LLM: > 5000ms

## Next Steps

### To Build the Project

1. Restore packages: `dotnet restore`
2. Build: `dotnet build`

### To Run the Application

1. Ensure MongoDB is running
2. Ensure Ollama is running
3. Run: `dotnet run`
4. Visit: `https://localhost:7285/statistics` for dashboard

## Known Issues

- Pre-existing build errors in the codebase (related to other files not in this PR)
- These errors existed before this logging implementation
- Logging files themselves compile correctly

## Benefits

1. **Observability**: Full visibility into application behavior
2. **Performance Monitoring**: Track and identify slow operations
3. **Debugging**: Correlation IDs enable request tracing
4. **Alerting**: Error detection and notification
5. **Dashboard**: Real-time metrics visualization

## Maintenance

### Adding New Metrics
```csharp
// In any service
_metricsCollector.RecordDatabaseQueryDuration("FindChunks", durationMs);
_metricsCollector.RecordLlmCallDuration(durationMs, isSuccess);
_metricsCollector.RecordRagOperation("GenerateResponse", durationMs);
```

### Adding New Log Patterns
```csharp
// Use extension methods or add new ones to StructuredLogExtensions
_logger.LogInformation("Custom operation {@Data}", customData);
```
