using BlazorApp.Data;
using BlazorApp.Models;
using Microsoft.Extensions.Options;

namespace BlazorApp.Services;

/// <summary>Coordina consultas RAG y operaciones de historial del usuario autenticado.</summary>
public sealed class HistoryService
{
    private readonly ChatSessionService _sessions;
    private readonly RagService _rag;
    private readonly CurrentUserContext _currentUser;
    private readonly HistoryOptions _options;

    public HistoryService(ChatSessionService sessions, RagService rag, CurrentUserContext currentUser, IOptions<HistoryOptions> options)
    {
        _sessions = sessions;
        _rag = rag;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(string? search = null, int page = 1, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        return string.IsNullOrWhiteSpace(search)
            ? await _sessions.GetUserSessionsAsync(page, 20, cancellationToken)
            : await _sessions.SearchSessionsAsync(search.Trim(), page, 20, cancellationToken);
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        return await _sessions.GetSessionAsync(sessionId, includeMessages: true, cancellationToken: cancellationToken);
    }

    public async Task ArchiveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        await _sessions.ArchiveSessionAsync(sessionId, cancellationToken);
    }

    public async Task RestoreAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        await _sessions.RestoreSessionAsync(sessionId, cancellationToken);
    }

    public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        await _sessions.DeleteSessionAsync(sessionId, cancellationToken);
    }

    public async Task<string> ExportJsonAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        return await _sessions.ExportSessionAsync(sessionId, cancellationToken);
    }

    public async Task<string> ExportMarkdownAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireActorAsync();
        return await _sessions.ExportSessionToMarkdownAsync(sessionId, cancellationToken);
    }

    public async Task<HistoryQueryResult> SubmitAsync(string query, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync();
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 1 || normalizedQuery.Length > _options.MaxQueryLength)
            throw new ArgumentException($"La consulta debe tener entre 1 y {_options.MaxQueryLength} caracteres.", nameof(query));

        ChatSession? session = null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            session = await GetSessionAsync(sessionId, cancellationToken);
            if (session is null) throw new InvalidOperationException("La sesión no existe o no pertenece al usuario actual.");
        }

        var sessionTitle = normalizedQuery.Length < 5
            ? "Nueva sesión"
            : (normalizedQuery.Length > 80 ? normalizedQuery[..80] : normalizedQuery);
        session ??= await _sessions.CreateSessionAsync(sessionTitle, cancellationToken: cancellationToken);
        session.GenerateTitleFromContent(normalizedQuery);
        await _sessions.UpdateSessionAsync(session, cancellationToken);

        var entryId = Guid.NewGuid().ToString("N");
        await _sessions.AddMessageAsync(ChatMessage.CreateUserMessage(normalizedQuery, session.Id, ownerUserId: actor.UserId, entryId: entryId), cancellationToken);

        try
        {
            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_options.OperationTimeoutSeconds > 0)
            {
                operationCts.CancelAfter(TimeSpan.FromSeconds(_options.OperationTimeoutSeconds));
            }
            var response = await _rag.QueryAsync(new RagQueryRequest { Query = normalizedQuery, SessionId = session.Id, CorrelationId = entryId }, operationCts.Token);
            var timedOut = operationCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            if (timedOut)
            {
                response = new RagResponse
                {
                    Status = RagResultStatus.TimedOut,
                    AnswerText = "La consulta agotó el tiempo de espera.",
                    Sources = response.Sources,
                    ModelMetadata = response.ModelMetadata,
                    FailureCode = "timeout"
                };
            }
            var status = response.Status switch
            {
                RagResultStatus.Succeeded or RagResultStatus.NoResults => EntryStatuses.Completed,
                RagResultStatus.Cancelled => EntryStatuses.Cancelled,
                RagResultStatus.TimedOut => EntryStatuses.TimedOut,
                _ => EntryStatuses.Failed
            };
            var assistant = ChatMessage.CreateAssistantMessage(response.AnswerText, session.Id,
                response.Sources.Select(source => source.DocumentId).Distinct().ToList(),
                ownerUserId: actor.UserId, entryId: entryId, sources: response.Sources);
            assistant.EntryStatus = status;
            assistant.IsReusable = response.IsValid;
            assistant.FailureCode = response.FailureCode;
            assistant.ModelMetadata = new Dictionary<string, string>
            {
                ["status"] = response.Status.ToString(),
                ["contextChunksUsed"] = (response.ModelMetadata?.ContextChunksUsed ?? 0).ToString(),
                ["retrievedAtUtc"] = (response.ModelMetadata?.RetrievedAtUtc ?? DateTime.UtcNow).ToString("O"),
                ["processingTimeMs"] = (response.ModelMetadata?.ProcessingTimeMs ?? 0).ToString()
            };
            await _sessions.AddMessageAsync(assistant, cancellationToken);
            return new HistoryQueryResult(session.Id, normalizedQuery, response.AnswerText, response);
        }
        catch (OperationCanceledException)
        {
            await _sessions.AddMessageAsync(ChatMessage.CreateSystemMessage("La consulta fue cancelada.", session.Id, actor.UserId, entryId, EntryStatuses.Cancelled), CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _sessions.AddMessageAsync(ChatMessage.CreateSystemMessage("No se pudo completar la consulta.", session.Id, actor.UserId, entryId), CancellationToken.None);
            throw new InvalidOperationException("No se pudo completar la consulta.", ex);
        }
    }

    private async Task<Data.ApplicationActor> RequireActorAsync() => await _currentUser.GetActorAsync()
        ?? throw new UnauthorizedAccessException("El usuario debe iniciar sesión.");
}

public sealed record HistoryQueryResult(string SessionId, string Query, string Response, RagResponse RagResponse);
