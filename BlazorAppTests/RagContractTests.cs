using BlazorApp.Models;
using Xunit;

namespace BlazorAppTests;

public sealed class RagContractTests
{
    [Fact]
    public void RagQueryRequest_HasCorrelationIdAndSafeDefaultLimit()
    {
        var request = new RagQueryRequest { Query = "¿Qué procedimiento aplica?" };

        Assert.False(string.IsNullOrWhiteSpace(request.CorrelationId));
        Assert.Equal(5, request.MaxResults);
    }

    [Fact]
    public void RagResponse_RetainsTypedSourcesAndModelMetadata()
    {
        var source = new SourceReference
        {
            DocumentId = "doc-1",
            StoredDocumentName = "manual.pdf",
            Page = 4,
            Section = "Introducción",
            Quote = "Texto citado",
            Relevance = 0.91
        };
        var response = new RagResponse
        {
            Status = RagResultStatus.Succeeded,
            AnswerText = "Respuesta",
            Sources = new[] { source },
            ModelMetadata = new RagModelMetadata { ModelName = "llama3.2", ContextChunksUsed = 1 }
        };

        Assert.Equal(RagResultStatus.Succeeded, response.Status);
        Assert.Same(source, Assert.Single(response.Sources));
        Assert.Equal("llama3.2", response.ModelMetadata.ModelName);
    }
}
