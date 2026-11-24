using BlazorApp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorAppTests;

public class DocumentChunkerTests
{
    [Fact]
    public void ChunkDocument_SplitsOversizedParagraphsWithinConfiguredLimit()
    {
        var chunker = new DocumentChunker(
            chunkSize: 1000,
            overlapSize: 200,
            logger: NullLogger<DocumentChunker>.Instance);
        var content = new string('A', 6000);

        var chunks = chunker.ChunkDocument(content, "document-1");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.InRange(chunk.ChunkText.Length, 1, 1000));
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.ChunkIndex));
        Assert.All(chunks, chunk => Assert.Equal("document-1", chunk.DocumentId));
    }
}
