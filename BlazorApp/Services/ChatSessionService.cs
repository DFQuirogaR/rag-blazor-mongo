using BlazorApp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Timers;
using System.Text.RegularExpressions;

namespace BlazorApp.Services;

/// <summary>
/// Servicio para gestionar sesiones de chat con persistencia en MongoDB
/// incluye limpieza automática y exportación de conversaciones.
/// </summary>
public class ChatSessionService : IHostedService, IDisposable
{
    private readonly ILogger<ChatSessionService> _logger;
    private readonly IMongoDatabase _database;
    private readonly CurrentUserContext _currentUser;
    private readonly System.Timers.Timer _cleanupTimer;
    private bool _isDisposed;
    private bool? _supportsTransactions;

    private IMongoCollection<ChatSession> SessionsCollection => _database.GetCollection<ChatSession>("chat_sessions");
    private IMongoCollection<ChatMessage> MessagesCollection => _database.GetCollection<ChatMessage>("chat_messages");

    /// <summary>
    /// Intervalo entre limpiezas automáticas en minutos (por defecto 60 minutos)
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Edad máxima de sesiones no archivadas antes de la limpieza (por defecto 30 días)
    /// </summary>
    public TimeSpan MaxInactiveAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Crea una nueva instancia del servicio de sesiones de chat.
    /// </summary>
    public ChatSessionService(
        ILogger<ChatSessionService> logger,
        IMongoDatabase database,
        CurrentUserContext currentUser)
    {
        _logger = logger;
        _database = database;
        _currentUser = currentUser;
        _cleanupTimer = new System.Timers.Timer();
        _cleanupTimer.Elapsed += CleanupTimer_Elapsed;
    }

    private async Task<Data.ApplicationActor> RequireOwnerAsync()
    {
        return await _currentUser.GetActorAsync()
            ?? throw new UnauthorizedAccessException("El usuario debe iniciar sesión.");
    }

    /// <summary>
    /// Crea una nueva sesión de chat
    /// </summary>
    public virtual async Task<ChatSession> CreateSessionAsync(
        string? title = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            var session = ChatSession.Create(
                title: title ?? "Nueva sesión de chat",
                userId: actor.UserId,
                tags: tags ?? new List<string>());
            session.OwnerEmailSnapshot = actor.Email;

            await SessionsCollection.InsertOneAsync(session, cancellationToken: cancellationToken);
            _logger.LogInformation("Nueva sesión creada: {SessionId}", session.Id);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear sesión de chat");
            throw;
        }
    }

    /// <summary>
    /// Obtiene una sesión por ID
    /// </summary>
    public virtual async Task<ChatSession?> GetSessionAsync(
        string sessionId,
        bool includeMessages = false,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            var filter = Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.Id, sessionId),
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Ne(s => s.Status, SessionStatuses.Deleted));
            var session = await SessionsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (session != null && includeMessages)
            {
                session = await LoadSessionMessagesAsync(session, cancellationToken);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sesión: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Obtiene todas las sesiones de un usuario
    /// </summary>
    public async Task<List<ChatSession>> GetUserSessionsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        try
        {
            var filter = Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Ne(s => s.Status, SessionStatuses.Deleted));
            var sessions = await SessionsCollection.Find(filter)
                .SortByDescending(s => s.LastActive)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);

            // Cargar mensajes si hay sesiones
            if (sessions.Any())
            {
                var sessionIds = sessions.Select(s => s.Id).ToList();
                var messages = await MessagesCollection
                    .Find(Builders<ChatMessage>.Filter.And(
                        Builders<ChatMessage>.Filter.In(m => m.SessionId, sessionIds),
                        Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId)))
                    .ToListAsync(cancellationToken);

                // Agrupar mensajes por sesión
                var messagesBySession = messages.GroupBy(m => m.SessionId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var session in sessions)
                {
                    if (messagesBySession.TryGetValue(session.Id, out var sessionMessages))
                    {
                        session.Messages = sessionMessages
                            .OrderBy(m => m.Timestamp)
                            .ToList();
                    }
                }
            }

            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sesiones del usuario: {UserId}", actor.UserId);
            throw;
        }
    }

    /// <summary>
    /// Carga mensajes de una sesión
    /// </summary>
    private async Task<ChatSession> LoadSessionMessagesAsync(
        ChatSession session,
        CancellationToken cancellationToken)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            var messages = await MessagesCollection
                .Find(Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SessionId, session.Id),
                    Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId)))
                .SortBy(m => m.Timestamp)
                .ToListAsync(cancellationToken);

            session.Messages = messages;
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cargar mensajes para sesión: {SessionId}", session.Id);
            return session;
        }
    }

    /// <summary>
    /// Obtiene mensajes de una sesión
    /// </summary>
    public async Task<List<ChatMessage>> GetSessionMessagesAsync(
        string sessionId,
        int skip = 0,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            // A deleted session is not readable, including through direct message queries.
            var session = await GetSessionAsync(sessionId, includeMessages: false, cancellationToken: cancellationToken);
            if (session is null)
            {
                return new List<ChatMessage>();
            }

            var messages = await MessagesCollection
                .Find(Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SessionId, sessionId),
                    Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId)))
                .SortBy(m => m.Timestamp)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync(cancellationToken);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener mensajes de sesión: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Agrega un mensaje a una sesión
    /// </summary>
    public async Task<ChatMessage> AddMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            // Resolve the session through the owner-filtered query and never trust UI ownership fields.
            var session = await GetSessionAsync(message.SessionId, false, cancellationToken);
            if (session == null)
                throw new InvalidOperationException($"La sesión {message.SessionId} no existe");

            message.OwnerUserId = actor.UserId;
            message.SessionId = session.Id;
            message.MessageIndex = (int)await MessagesCollection.CountDocumentsAsync(
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SessionId, session.Id),
                    Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId)),
                cancellationToken: cancellationToken);
            await MessagesCollection.InsertOneAsync(message, cancellationToken: cancellationToken);

            await SessionsCollection.UpdateOneAsync(
                Builders<ChatSession>.Filter.And(
                    Builders<ChatSession>.Filter.Eq(s => s.Id, session.Id),
                    Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId)),
                Builders<ChatSession>.Update.Set(s => s.LastActive, DateTime.UtcNow),
                cancellationToken: cancellationToken);

            _logger.LogDebug("Mensaje agregado a sesión {SessionId}: {MessageIndex}", 
                message.SessionId, message.Id);

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar mensaje a sesión: {SessionId}", message.SessionId);
            throw;
        }
    }

    /// <summary>
    /// Actualiza una sesión existente
    /// </summary>
    public async Task UpdateSessionAsync(
        ChatSession session,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            var filter = Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.Id, session.Id),
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Ne(s => s.Status, SessionStatuses.Deleted));
            var update = Builders<ChatSession>.Update
                .Set(s => s.Title, session.Title)
                .Set(s => s.LastActive, session.LastActive)
                .Set(s => s.Metadata, session.Metadata);

            await SessionsCollection.UpdateOneAsync(
                filter,
                update,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Sesión actualizada: {SessionId}", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar sesión: {SessionId}", session.Id);
            throw;
        }
    }

    /// <summary>
    /// Archiva una sesión
    /// </summary>
    public async Task ArchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        await SessionsCollection.UpdateOneAsync(
            Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.Id, sessionId),
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Ne(s => s.Status, SessionStatuses.Deleted)),
            Builders<ChatSession>.Update.Set(s => s.Status, SessionStatuses.Archived)
                .Set(s => s.LastActive, DateTime.UtcNow), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Recupera una sesión archivada
    /// </summary>
    public async Task RestoreSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        await SessionsCollection.UpdateOneAsync(
            Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.Id, sessionId),
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Eq(s => s.Status, SessionStatuses.Archived)),
            Builders<ChatSession>.Update.Set(s => s.Status, SessionStatuses.Active)
                .Set(s => s.LastActive, DateTime.UtcNow), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Elimina una sesión y sus mensajes
    /// </summary>
    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        var sessionFilter = Builders<ChatSession>.Filter.And(
            Builders<ChatSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
            Builders<ChatSession>.Filter.Ne(s => s.Status, SessionStatuses.Deleted));

        // Without a replica set MongoDB cannot atomically delete both collections.
        // Marking the owner-scoped session deleted keeps the messages inaccessible and coherent.
        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await SessionsCollection.UpdateOneAsync(sessionFilter,
                Builders<ChatSession>.Update.Set(s => s.Status, SessionStatuses.Deleted),
                cancellationToken: cancellationToken);
            return;
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            await MessagesCollection.DeleteManyAsync(session,
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SessionId, sessionId),
                    Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId)),
                cancellationToken: cancellationToken);
            await SessionsCollection.DeleteOneAsync(session, sessionFilter, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> SupportsTransactionsAsync(CancellationToken cancellationToken)
    {
        if (_supportsTransactions.HasValue) return _supportsTransactions.Value;
        try
        {
            var hello = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);
            _supportsTransactions = hello.Contains("setName") || hello.GetValue("msg", "").AsString == "isdbgrid";
        }
        catch (MongoCommandException)
        {
            _supportsTransactions = false;
        }
        return _supportsTransactions.Value;
    }

    /// <summary>
    /// Busca sesiones por título o contenido de mensajes
    /// </summary>
    public async Task<List<ChatSession>> SearchSessionsAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            var filter = Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Ne(s => s.Status, SessionStatuses.Deleted));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var escaped = Regex.Escape(query);
                var titleFilter = Builders<ChatSession>.Filter.Regex(
                    s => s.Title, new BsonRegularExpression(escaped, "i"));
                var messageIds = await MessagesCollection.Find(
                    Builders<ChatMessage>.Filter.And(
                        Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId),
                        Builders<ChatMessage>.Filter.Regex(m => m.Content, new BsonRegularExpression(escaped, "i"))))
                    .Project(m => m.SessionId).ToListAsync(cancellationToken);
                filter = Builders<ChatSession>.Filter.And(filter,
                    Builders<ChatSession>.Filter.Or(titleFilter,
                        Builders<ChatSession>.Filter.In(s => s.Id, messageIds.Distinct())));
            }

            var sessions = await SessionsCollection
                .Find(filter)
                .SortByDescending(s => s.LastActive)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar sesiones: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Limpia sesiones antiguas (automático o manual)
    /// </summary>
    public async Task<int> CleanupOldSessionsAsync(
        TimeSpan? maxAge = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(maxAge ?? MaxInactiveAge);
            var filter = Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId),
                Builders<ChatSession>.Filter.Lt(s => s.LastActive, cutoffDate),
                Builders<ChatSession>.Filter.Eq(s => s.Status, SessionStatuses.Active));

            var oldSessions = await SessionsCollection
                .Find(filter)
                .Project(s => s.Id)
                .ToListAsync(cancellationToken);

            int deletedCount = 0;
            
            foreach (var sessionId in oldSessions)
            {
                try
                {
                    await DeleteSessionAsync(sessionId, cancellationToken);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al eliminar sesión antigua: {SessionId}", sessionId);
                }
            }

            _logger.LogInformation("Limpieza de sesiones antiguas completada: {DeletedCount} sesiones eliminadas", deletedCount);
            
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en limpieza de sesiones antiguas");
            throw;
        }
    }

    /// <summary>
    /// Exporta una sesión a formato JSON
    /// </summary>
    public async Task<string> ExportSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, true, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"La sesión {sessionId} no existe");
            }

            var exportData = new
            {
                Session = new
                {
                    session.Id,
                    session.Title,
                    session.CreatedDate,
                    session.LastActive,
                    session.UserId,
                    session.Metadata
                },
                Messages = session.Messages.Select(m => new
                {
                    m.Id,
                    m.Role,
                    m.Content,
                    m.Timestamp,
                    m.SourceDocuments,
                    m.Metadata
                }),
                ExportDate = DateTime.UtcNow,
                MessageCount = session.Messages.Count,
                TotalTokens = session.Messages.Sum(m => m.Metadata?.TokensUsed ?? 0)
            };

            return System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar sesión: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Exporta una sesión a formato Markdown
    /// </summary>
    public async Task<string> ExportSessionToMarkdownAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, true, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"La sesión {sessionId} no existe");
            }

            var markdown = new System.Text.StringBuilder();
            markdown.AppendLine($"# {session.Title}");
            markdown.AppendLine();
            markdown.AppendLine($"**ID:** {session.Id}");
            markdown.AppendLine($"**Creado:** {session.CreatedDate:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine($"**Última actividad:** {session.LastActive:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine();

            if (!string.IsNullOrWhiteSpace(session.UserId))
            {
                markdown.AppendLine($"**Usuario:** {session.UserId}");
                markdown.AppendLine();
            }

            markdown.AppendLine("---");
            markdown.AppendLine();
            markdown.AppendLine("## Historial de Conversación");
            markdown.AppendLine();

            foreach (var message in session.Messages.OrderBy(m => m.Timestamp))
            {
                markdown.AppendLine($"### [{message.Role.ToUpper()}] {message.Timestamp:yyyy-MM-dd HH:mm:ss}");
                markdown.AppendLine();
                markdown.AppendLine(message.Content);
                markdown.AppendLine();

                if (message.SourceDocuments?.Any() == true)
                {
                    markdown.AppendLine("**Fuentes:**");
                    foreach (var source in message.SourceDocuments)
                    {
                        markdown.AppendLine($"- {source}");
                    }
                    markdown.AppendLine();
                }
            }

            return markdown.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar sesión a Markdown: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Obtiene estadísticas de sesiones
    /// </summary>
    public async Task<SessionStatistics> GetSessionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireOwnerAsync();
        var ownerSessionFilter = Builders<ChatSession>.Filter.Eq(s => s.OwnerUserId, actor.UserId);
        var ownerMessageFilter = Builders<ChatMessage>.Filter.Eq(m => m.OwnerUserId, actor.UserId);
        try
        {
            var totalSessions = await SessionsCollection.CountDocumentsAsync(ownerSessionFilter, cancellationToken: cancellationToken);
            var archivedSessions = await SessionsCollection.CountDocumentsAsync(
                Builders<ChatSession>.Filter.And(ownerSessionFilter, Builders<ChatSession>.Filter.Eq(s => s.Status, SessionStatuses.Archived)),
                cancellationToken: cancellationToken);
            var totalMessages = await MessagesCollection.CountDocumentsAsync(ownerMessageFilter, cancellationToken: cancellationToken);

            var oldestSession = await SessionsCollection.Find(ownerSessionFilter).SortBy(s => s.CreatedDate).Limit(1).FirstOrDefaultAsync(cancellationToken);
            var newestSession = await SessionsCollection.Find(ownerSessionFilter).SortByDescending(s => s.CreatedDate).Limit(1).FirstOrDefaultAsync(cancellationToken);

            var messageCountsBySession = await MessagesCollection.Find(ownerMessageFilter)
                .Project(m => m.SessionId).ToListAsync(cancellationToken);
            var avgMessagesPerSession = messageCountsBySession.Count == 0
                ? 0
                : messageCountsBySession.GroupBy(id => id).Average(group => group.Count());

            return new SessionStatistics
            {
                TotalSessions = (int)totalSessions,
                ArchivedSessions = (int)archivedSessions,
                TotalMessages = (int)totalMessages,
                AverageMessagesPerSession = avgMessagesPerSession,
                OldestSessionDate = oldestSession?.CreatedDate,
                NewestSessionDate = newestSession?.CreatedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estadísticas de sesiones");
            throw;
        }
    }

    /// <summary>
    /// Configura índices de MongoDB incluyendo TTL para limpieza automática
    /// </summary>
    public async Task ConfigureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionIndex = new CreateIndexModel<ChatSession>(
                Builders<ChatSession>.IndexKeys.Ascending(s => s.OwnerUserId)
                    .Ascending(s => s.Status).Descending(s => s.LastActive),
                new CreateIndexOptions { Name = "owner_status_last_active_v2" });
            await SessionsCollection.Indexes.CreateOneAsync(sessionIndex, cancellationToken: cancellationToken);

            var messageIndex = new CreateIndexModel<ChatMessage>(
                Builders<ChatMessage>.IndexKeys.Ascending(m => m.OwnerUserId)
                    .Ascending(m => m.SessionId).Ascending(m => m.Timestamp),
                new CreateIndexOptions { Name = "owner_session_timestamp_v2" });
            await MessagesCollection.Indexes.CreateOneAsync(messageIndex, cancellationToken: cancellationToken);

            _logger.LogInformation("Índices de MongoDB configurados correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al configurar índices de MongoDB");
            throw;
        }
    }

    /// <summary>
    /// Implementación de IHostedService - Inicia el servicio de limpieza automática
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Configurar índices al inicio
            await ConfigureIndexesAsync(cancellationToken);

            // Iniciar temporizador de limpieza automática
            _cleanupTimer.Interval = CleanupIntervalMinutes * 60 * 1000; // Convertir a milisegundos
            _cleanupTimer.Enabled = true;

            _logger.LogInformation("ChatSessionService iniciado. Limpieza automática cada {Minutes} minutos", CleanupIntervalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar ChatSessionService");
            throw;
        }
    }

    /// <summary>
    /// Implementación de IHostedService - Detiene el servicio
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cleanupTimer.Enabled = false;
            _logger.LogInformation("ChatSessionService detenido");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detener ChatSessionService");
            throw;
        }
    }

    /// <summary>
    /// Evento del temporizador de limpieza automática
    /// </summary>
    private async void CleanupTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Iniciando limpieza automática de sesiones...");
            var deletedCount = await CleanupOldSessionsAsync();
            _logger.LogInformation("Limpieza automática completada. {Count} sesiones eliminadas", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en limpieza automática de sesiones");
        }
    }

    /// <summary>
    /// Libera recursos
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cleanupTimer?.Stop();
        _cleanupTimer?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~ChatSessionService()
    {
        Dispose();
    }
}

/// <summary>
/// Estadísticas de sesiones
/// </summary>
public class SessionStatistics
{
    public int TotalSessions { get; set; }
    public int ArchivedSessions { get; set; }
    public int TotalMessages { get; set; }
    public double AverageMessagesPerSession { get; set; }
    public DateTime? OldestSessionDate { get; set; }
    public DateTime? NewestSessionDate { get; set; }

    public override string ToString()
    {
        return $"Sesiones: {TotalSessions} total, {ArchivedSessions} archivadas, " +
               $"Mensajes: {TotalMessages}, Promedio: {AverageMessagesPerSession:F2} por sesión";
    }
}
