using System.Security.Claims;
using BlazorApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace BlazorAppTests;

public sealed class ChatSessionPersistenceIsolationTests
{
    [Fact]
    public async Task Sessions_are_isolated_between_two_authenticated_local_users()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return; // Mongo integration tests run when the Docker Mongo service is configured.

        var databaseName = $"rag_isolation_{Guid.NewGuid():N}";
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);

        try
        {
            var alice = CreateService(database, "local-alice", "alice@example.test");
            var bob = CreateService(database, "local-bob", "bob@example.test");
            await alice.ConfigureIndexesAsync();

            var sessionIndexNames = await database.GetCollection<BlazorApp.Models.ChatSession>("chat_sessions")
                .Indexes.List().ToListAsync();
            var messageIndexNames = await database.GetCollection<BlazorApp.Models.ChatMessage>("chat_messages")
                .Indexes.List().ToListAsync();
            Assert.Contains(sessionIndexNames, index => index["name"].AsString == "owner_status_last_active_v2");
            Assert.Contains(messageIndexNames, index => index["name"].AsString == "owner_session_timestamp_v2");

            var aliceSession = await alice.CreateSessionAsync("Alice private session");
            await alice.AddMessageAsync(BlazorApp.Models.ChatMessage.CreateUserMessage(
                "Alice private query", aliceSession.Id));

            var bobSessions = await bob.GetUserSessionsAsync();
            var bobCannotOpenAliceSession = await bob.GetSessionAsync(aliceSession.Id, includeMessages: true);
            var aliceSessions = await alice.GetUserSessionsAsync();

            Assert.DoesNotContain(bobSessions, session => session.Id == aliceSession.Id);
            Assert.Null(bobCannotOpenAliceSession);
            Assert.Contains(aliceSessions, session => session.Id == aliceSession.Id);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static ChatSessionService CreateService(IMongoDatabase database, string userId, string email)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email)
        }, authenticationType: "Test"));
        var provider = new TestAuthenticationStateProvider(principal);
        return new ChatSessionService(
            NullLogger<ChatSessionService>.Instance,
            database,
            new CurrentUserContext(provider));
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public TestAuthenticationStateProvider(ClaimsPrincipal principal)
        {
            _state = new AuthenticationState(principal);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }
}
