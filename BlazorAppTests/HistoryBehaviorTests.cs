using BlazorApp.Models;
using Xunit;

namespace BlazorAppTests;

public sealed class HistoryBehaviorTests
{
    [Fact]
    public void Adding_message_sets_session_owner_and_updates_activity()
    {
        var session = ChatSession.Create("Consulta", "owner-a");
        var before = session.LastActive;
        var message = ChatMessage.CreateUserMessage("Pregunta", "otro-id");

        session.AddMessage(message);

        Assert.Equal(session.Id, message.SessionId);
        Assert.Equal("owner-a", message.OwnerUserId);
        Assert.Single(session.Messages);
        Assert.True(session.LastActive >= before);
    }

    [Fact]
    public void Failed_response_is_not_reusable_and_keeps_failure_state()
    {
        var session = ChatSession.Create("Consulta", "owner-a");
        var message = ChatMessage.CreateAssistantMessage("No se pudo completar la consulta.", session.Id, ownerUserId: "owner-a");
        message.EntryStatus = EntryStatuses.Failed;
        message.IsReusable = false;
        message.FailureCode = "rag-failure";

        session.AddMessage(message);

        Assert.Equal(EntryStatuses.Failed, session.Messages[0].EntryStatus);
        Assert.False(session.Messages[0].IsReusable);
        Assert.Equal("rag-failure", session.Messages[0].FailureCode);
    }

    [Fact]
    public void Conversation_history_is_chronological_and_respects_limit()
    {
        var session = ChatSession.Create("Consulta", "owner-a");
        var first = ChatMessage.CreateUserMessage("Primera", session.Id, ownerUserId: "owner-a");
        var answer = ChatMessage.CreateAssistantMessage("Respuesta", session.Id, ownerUserId: "owner-a");
        var second = ChatMessage.CreateUserMessage("Segunda", session.Id, ownerUserId: "owner-a");
        first.Timestamp = DateTime.UtcNow.AddMinutes(-2);
        answer.Timestamp = DateTime.UtcNow.AddMinutes(-1);
        second.Timestamp = DateTime.UtcNow;
        session.Messages.Add(first);
        session.Messages.Add(answer);
        session.Messages.Add(second);

        var history = session.GetConversationHistory(maxMessages: 2);

        Assert.DoesNotContain("Primera", history);
        Assert.Contains("Respuesta", history);
        Assert.Contains("Segunda", history);
        Assert.True(history.IndexOf("Respuesta", StringComparison.Ordinal) < history.IndexOf("Segunda", StringComparison.Ordinal));
    }
}
