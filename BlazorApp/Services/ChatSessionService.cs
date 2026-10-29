using BlazorApp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Timers;

namespace BlazorApp.Services;

/// <summary>
/// Servicio para gestionar sesiones de chat con persistencia en MongoDB
/// incluye limpieza automática y exportación de conversaciones.
/// </summary>
public class ChatSessionService : IHostedService, IDisposable
{
    private readonly ILogger<ChatSessionService> _logger;
    private readonly IMongoDatabase _database;
    private readonly System.Timers.Timer _cleanupTimer;
    private bool _isDisposed;

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
        IMongoDatabase database)
    {
        _logger = logger;
        _database = database;
        _cleanupTimer = new System.Timers.Timer();
        _cleanupTimer.Elapsed += CleanupTimer_Elapsed;
    }

    /// <summary>
    /// Crea una nueva sesión de chat
    /// </summary>
    public async Task<ChatSession> CreateSessionAsync(
        string? title = null,
        string? userId = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = ChatSession.Create(
                title: title ?? "Nueva sesión de chat",
                userId: userId,
                tags: tags ?? new List<string>());

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
    public async Task<ChatSession?> GetSessionAsync(
        string sessionId,
        bool includeMessages = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await SessionsCollection
                .Find(s => s.Id == sessionId)
                .FirstOrDefaultAsync(cancellationToken);

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
        string? userId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = userId == null
                ? Builders<ChatSession>.Filter.Empty
                : Builders<ChatSession>.Filter.Eq(s => s.UserId, userId);

            var sessions = await SessionsCollection
                .Find(filter)
                .SortByDescending(s => s.LastActive)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            // Cargar mensajes si hay sesiones
            if (sessions.Any())
            {
                var sessionIds = sessions.Select(s => s.Id).ToList();
                var messages = await MessagesCollection
                    .Find(m => sessionIds.Contains(m.SessionId))
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
            _logger.LogError(ex, "Error al obtener sesiones del usuario: {UserId}", userId);
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
        try
        {
            var messages = await MessagesCollection
                .Find(m => m.SessionId == session.Id)
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
        try
        {
            var messages = await MessagesCollection
                .Find(m => m.SessionId == sessionId)
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
        try
        {
            // Verificar que la sesión existe
            var session = await GetSessionAsync(message.SessionId, false, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"La sesión {message.SessionId} no existe");
            }

            // Insertar el mensaje
            await MessagesCollection.InsertOneAsync(message, cancellationToken: cancellationToken);

            // Actualizar timestamp de última actividad
            await SessionsCollection.UpdateOneAsync(
                s => s.Id == message.SessionId,
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
        try
        {
            var filter = Builders<ChatSession>.Filter.Eq(s => s.Id, session.Id);
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
    public async Task ArchiveSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = Builders<ChatSession>.Update
                .Set(s => s.Metadata.CustomProperties["status"], "archived")
                .Set(s => s.LastActive, DateTime.UtcNow);

            await SessionsCollection.UpdateOneAsync(
                s => s.Id == sessionId,
                update,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Sesión archivada: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al archivar sesión: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Recupera una sesión archivada
    /// </summary>
    public async Task RestoreSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = Builders<ChatSession>.Update
                .Unset(s => s.Metadata.CustomProperties["status"])
                .Set(s => s.LastActive, DateTime.UtcNow);

            await SessionsCollection.UpdateOneAsync(
                s => s.Id == sessionId,
                update,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Sesión restaurada: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al restaurar sesión: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Elimina una sesión y sus mensajes
    /// </summary>
    public async Task DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Eliminar mensajes primero
            await MessagesCollection.DeleteManyAsync(
                m => m.SessionId == sessionId,
                cancellationToken);

            // Eliminar sesión
            await SessionsCollection.DeleteOneAsync(
                s => s.Id == sessionId,
                cancellationToken);

            _logger.LogInformation("Sesión eliminada: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar sesión: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Busca sesiones por título o contenido de mensajes
    /// </summary>
    public async Task<List<ChatSession>> SearchSessionsAsync(
        string query,
        string? userId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<ChatSession>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var queryRegex = new MongoDB.Bson.BsonRegularExpression(query, "i");
                var titleFilter = Builders<ChatSession>.Filter.Regex(s => s.Title, queryRegex);
                
                // Buscar también en contenido de mensajes
                var messageContentFilter = Builders<ChatSession>.Filter.ElemMatch(
                    s => s.Messages,
                    m => m.Content.Contains(query));

                filter = Builders<ChatSession>.Filter.Or(titleFilter, messageContentFilter);
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                filter = Builders<ChatSession>.Filter.And(
                    filter,
                    Builders<ChatSession>.Filter.Eq(s => s.UserId, userId));
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
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(maxAge ?? MaxInactiveAge);
            
            // Buscar sesiones inactivas que no tengan status "archived"
            var filter = Builders<ChatSession>.Filter.And(
                Builders<ChatSession>.Filter.Lt(s => s.LastActive, cutoffDate),
                Builders<ChatSession>.Filter.Or(
                    Builders<ChatSession>.Filter.Eq(s => s.Metadata.CustomProperties["status"], "archived"),
                    Builders<ChatSession>.Filter.Not(Builders<ChatSession>.Filter.Exists(s => s.Metadata.CustomProperties["status"]))
                )
            );

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
        try
        {
            var totalSessions = await SessionsCollection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
            var archivedSessions = await SessionsCollection.CountDocumentsAsync(
                s => s.Metadata != null && s.Metadata.CustomProperties != null && s.Metadata.CustomProperties["status"] == "archived",
                cancellationToken: cancellationToken);
            var totalMessages = await MessagesCollection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);

            var oldestSession = await SessionsCollection
                .Find(_ => true)
                .SortBy(s => s.CreatedDate)
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken);

            var newestSession = await SessionsCollection
                .Find(_ => true)
                .SortByDescending(s => s.CreatedDate)
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken);

            // Obtener promedio de mensajes por sesión (conteo agrupado por SessionId)
            var messageCountsBySession = await MessagesCollection
                .Aggregate()
                .Group(m => m.SessionId, g => new { SessionId = g.Key, MessageCount = g.Count() })
                .ToListAsync(cancellationToken);

            var avgMessagesPerSession = messageCountsBySession.Any() 
                ? messageCountsBySession.Average(s => s.MessageCount) 
                : 0;

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
            // Índice TTL para limpieza automática de sesiones inactivas (30 días)
            var ttlIndexOptions = new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.FromDays(30)
            };

            var ttlIndexDefinition = Builders<ChatSession>.IndexKeys.Ascending(s => s.LastActive);

            var ttlIndexModel = new CreateIndexModel<ChatSession>(
                ttlIndexDefinition,
                new CreateIndexOptions
                {
                    Background = true,
                    ExpireAfter = TimeSpan.FromDays(30)
                });

            await SessionsCollection.Indexes.CreateOneAsync(ttlIndexModel, cancellationToken: cancellationToken);

            // Índice para búsqueda por usuario
            var userIndexModel = new CreateIndexModel<ChatSession>(
                Builders<ChatSession>.IndexKeys.Ascending(s => s.UserId).Ascending(s => s.LastActive),
                new CreateIndexOptions { Background = true });

            await SessionsCollection.Indexes.CreateOneAsync(userIndexModel, cancellationToken: cancellationToken);

            // Índice para búsqueda de mensajes por sesión
            var messageIndexModel = new CreateIndexModel<ChatMessage>(
                Builders<ChatMessage>.IndexKeys.Ascending(m => m.SessionId).Ascending(m => m.Timestamp),
                new CreateIndexOptions { Background = true });

            await MessagesCollection.Indexes.CreateOneAsync(messageIndexModel, cancellationToken: cancellationToken);

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
