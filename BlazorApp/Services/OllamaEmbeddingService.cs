using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Text.Json;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Custom embedding service that talks directly to Ollama API
/// </summary>
public class OllamaEmbeddingService : ITextEmbeddingGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        string modelName,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _modelName = modelName;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ReadOnlyMemory<float>>();

        foreach (var text in data)
        {
            var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
            result.Add(embedding);
        }

        return result;
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                model = _modelName,
                prompt = text
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

            var response = await _httpClient.PostAsync("/api/embeddings", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody);

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Ollama returned empty embedding");
            }

            _logger.LogDebug("Generated embedding with {Dimensions} dimensions", result.Embedding.Length);

            return new ReadOnlyMemory<float>(result.Embedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with Ollama for text: {Text}", 
                text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            throw;
        }
    }

    private class OllamaEmbeddingResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
