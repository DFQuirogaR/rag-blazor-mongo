using BlazorApp.Components;
using BlazorApp.Logging;
using BlazorApp.Middleware;
using BlazorApp.Services;
using MongoDB.Driver;
using Microsoft.SemanticKernel;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RAG-Blazor")
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/rag-blazor-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add metrics collector
builder.Services.AddMetricsCollector();

// Add health checks (backs the container HEALTHCHECK at /health)
builder.Services.AddHealthChecks();

// Required by EmbeddingCacheService (in-memory tier of the hybrid embedding cache)
builder.Services.AddMemoryCache();

// Configure application logging levels
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// MongoDB
var mongoConnectionString = builder.Configuration["ConnectionStrings:MongoDb"] ?? "mongodb://mongodb:27017";
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("RagDatabase");
});

// Semantic Kernel with Ollama
var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://ollama:11434";
var ollamaModel = builder.Configuration["Ollama:Model"] ?? "llama3.2";
var embeddingModel = builder.Configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";

// Create a minimal kernel (without pre-configured services)
var kernel = Kernel.CreateBuilder().Build();
builder.Services.AddSingleton(kernel);

// Register custom Ollama chat service (direct API integration)
builder.Services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OllamaChatService>>();
    var chatHttpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaEndpoint),
        Timeout = TimeSpan.FromMinutes(5)
    };
    return new OllamaChatService(chatHttpClient, ollamaModel, logger);
});

// Register custom Ollama embedding service (direct API integration)
builder.Services.AddSingleton<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OllamaEmbeddingService>>();
    var embeddingHttpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaEndpoint),
        Timeout = TimeSpan.FromMinutes(5)
    };
    return new OllamaEmbeddingService(embeddingHttpClient, embeddingModel, logger);
});

// Register text extractors
builder.Services.AddSingleton<ITextExtractor, PdfTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, WordTextExtractor>();

// Register RAG services
builder.Services.AddSingleton<DocumentChunker>();
builder.Services.AddSingleton<DocumentProcessor>();
builder.Services.AddSingleton<DocumentManagementService>();
builder.Services.AddSingleton<EnhancedRagService>();
builder.Services.AddSingleton<ResponseFormatter>();
builder.Services.AddSingleton<IEmbeddingCache, EmbeddingCacheService>();
builder.Services.AddSingleton<RagOrchestrator>();

// Register chat session service
builder.Services.AddSingleton<ChatSessionService>();

// Register document scanner configuration
builder.Services.Configure<DocumentScannerOptions>(builder.Configuration.GetSection("DocumentSettings"));

// Register document scanner as a singleton so the admin page can trigger a scan.
// Register the same instance as the hosted service for startup monitoring.
builder.Services.AddSingleton<DocumentScanner>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DocumentScanner>());

builder.Services.AddSingleton<RagService>();

var app = builder.Build();

// Configure middleware pipeline
app.UseErrorHandling();
app.UseRequestLogging();
app.UsePerformanceMonitoring();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseWebSockets();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapHealthChecks("/health");

// Map generated static web assets, including the Blazor Web client bootstrap.
// Without this endpoint, /_framework/blazor.web.js returns 404 and pages remain static.
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
