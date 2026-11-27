using BlazorApp.Models;
using MongoDB.Bson;
using Xunit;

namespace BlazorAppTests;

public sealed class ChatSessionSerializationTests
{
    [Fact]
    public void ChatSession_AllowsIdentityGuidAsOwnerAndLegacyUserId()
    {
        var identityId = Guid.NewGuid().ToString();
        var session = ChatSession.Create("Consulta", identityId);

        var document = session.ToBsonDocument();

        Assert.Equal(identityId, document[nameof(ChatSession.OwnerUserId)].AsString);
        Assert.Equal(identityId, document["userId"].AsString);
    }
}
