using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

namespace BlazorApp.Services;

/// <summary>
/// Custom chat service that talks directly to Ollama API
/// </summary>
public class OllamaChatService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly ILogger<OllamaChatService> _logger;

    public OllamaChatService(
        HttpClient httpClient,
        string modelName,
        ILogger<OllamaChatService> logger)
    {
        _httpClient = httpClient;
        _modelName = modelName;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert chat history to Ollama format
            var messages = chatHistory.Select(m => new
            {
                role = m.Role.Label.ToLowerInvariant(),
                content = m.Content
            }).ToList();

            var request = new
            {
                model = _modelName,
                messages = messages,
                stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending chat request to Ollama with {MessageCount} messages", messages.Count);

            var response = await _httpClient.PostAsync("/api/chat", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody);

            if (result?.Message?.Content == null)
            {
                throw new InvalidOperationException("Ollama returned empty response");
            }

            _logger.LogInformation("Received response from Ollama: {Length} characters", result.Message.Content.Length);

            return new List<ChatMessageContent>
            {
                new ChatMessageContent(AuthorRole.Assistant, result.Message.Content)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama chat API");
            throw;
        }
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // For now, just return the complete response as a single chunk
        var contents = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var content in contents)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, content.Content);
        }
    }

    private class OllamaChatResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("role")]
        public string? Role { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
