using BlazorApp.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlazorApp.Services;

/// <summary>
/// Servicio para gestionar sesiones de chat
/// </summary>
public class ChatSessionService
{
    private readonly ILogger<ChatSessionService> _logger;
    private readonly IMongoDatabase _database;

    private IMongoCollection<ChatSessionModel> SessionsCollection => _database.GetCollection<ChatSessionModel>("chat_sessions");
    private IMongoCollection<ChatMessageModel> MessagesCollection => _database.GetCollection<ChatMessageModel>("chat_messages");

    public ChatSessionService(
        ILogger<ChatSessionService> logger,
        IMongoDatabase database)
    {
        _logger = logger;
        _database = database;
    }

    /// <summary>
    /// Crea una nueva sesión de chat
    /// </summary>
    public async Task<ChatSessionModel> CreateSessionAsync(
        string? title = null,
        string? userId = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = new ChatSessionModel
            {
                Title = title ?? "Nueva sesión de chat",
                UserId = userId,
                Tags = tags ?? new List<string>(),
                CreatedDate = DateTime.UtcNow,
                LastActivityDate = DateTime.UtcNow,
                Status = "active",
                MessageCount = 0
            };

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
    public async Task<ChatSessionModel?> GetSessionAsync(
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
    public async Task<List<ChatSessionModel>> GetUserSessionsAsync(
        string? userId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = userId == null
                ? Builders<ChatSessionModel>.Filter.Empty
                : Builders<ChatSessionModel>.Filter.Eq(s => s.UserId, userId);

            var sessions = await SessionsCollection
                .Find(filter)
                .SortByDescending(s => s.LastActivityDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

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
    private async Task<ChatSessionModel> LoadSessionMessagesAsync(
        ChatSessionModel session,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = await GetSessionMessagesAsync(session.Id, 0, 100, cancellationToken);
            // Aquí podríamos agregar los mensajes a un campo de navegación si fuera necesario
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
    public async Task<List<ChatMessageModel>> GetSessionMessagesAsync(
        string sessionId,
        int skip = 0,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = await MessagesCollection
                .Find(m => m.SessionId == sessionId)
                .SortBy(m => m.MessageIndex)
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
    public async Task<ChatMessageModel> AddMessageAsync(
        ChatMessageModel message,
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

            // Actualizar estadísticas de la sesión
            await UpdateSessionStatsAsync(message.SessionId, cancellationToken);

            _logger.LogDebug("Mensaje agregado a sesión {SessionId}: {MessageIndex}", 
                message.SessionId, message.MessageIndex);

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar mensaje a sesión: {SessionId}", message.SessionId);
            throw;
        }
    }

    /// <summary>
    /// Actualiza estadísticas de una sesión
    /// </summary>
    private async Task UpdateSessionStatsAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Contar mensajes en la sesión
            var messageCount = await MessagesCollection
                .CountDocumentsAsync(m => m.SessionId == sessionId, cancellationToken: cancellationToken);

            // Obtener último mensaje
            var lastMessage = await MessagesCollection
                .Find(m => m.SessionId == sessionId)
                .SortByDescending(m => m.MessageIndex)
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken);

            // Actualizar sesión
            var update = Builders<ChatSessionModel>.Update
                .Set(s => s.MessageCount, (int)messageCount)
                .Set(s => s.LastActivityDate, lastMessage?.Timestamp ?? DateTime.UtcNow);

            if (lastMessage != null && string.IsNullOrEmpty(lastMessage.Content))
            {
                // Actualizar título basado en el primer mensaje si no está establecido
                var firstMessage = await MessagesCollection
                    .Find(m => m.SessionId == sessionId && m.Role == "user")
                    .SortBy(m => m.MessageIndex)
                    .Limit(1)
                    .FirstOrDefaultAsync(cancellationToken);

                if (firstMessage != null)
                {
                    update = update.Set(s => s.Title, firstMessage.Content.Length > 50 
                        ? firstMessage.Content.Substring(0, 50) + "..." 
                        : firstMessage.Content);
                }
            }

            await SessionsCollection.UpdateOneAsync(
                s => s.Id == sessionId,
                update,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al actualizar estadísticas de sesión: {SessionId}", sessionId);
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
            var update = Builders<ChatSessionModel>.Update
                .Set(s => s.Status, "archived")
                .Set(s => s.LastActivityDate, DateTime.UtcNow);

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
    /// Limpia sesiones antiguas
    /// </summary>
    public async Task<int> CleanupOldSessionsAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(maxAge);
            
            // Buscar sesiones antiguas archivadas o inactivas
            var filter = Builders<ChatSessionModel>.Filter.And(
                Builders<ChatSessionModel>.Filter.Lt(s => s.LastActivityDate, cutoffDate),
                Builders<ChatSessionModel>.Filter.Or(
                    Builders<ChatSessionModel>.Filter.Eq(s => s.Status, "archived"),
                    Builders<ChatSessionModel>.Filter.Eq(s => s.Status, "inactive")
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

            var messages = await GetSessionMessagesAsync(sessionId, 0, 1000, cancellationToken);
            
            var exportData = new
            {
                Session = session,
                Messages = messages,
                ExportDate = DateTime.UtcNow,
                MessageCount = messages.Count,
                TotalTokens = messages.Sum(m => m.Metadata.TokensUsed ?? 0)
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
    /// Obtiene estadísticas de sesiones
    /// </summary>
    public async Task<SessionStatistics> GetSessionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var totalSessions = await SessionsCollection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
            var activeSessions = await SessionsCollection.CountDocumentsAsync(
                s => s.Status == "active", 
                cancellationToken: cancellationToken);
            var archivedSessions = await SessionsCollection.CountDocumentsAsync(
                s => s.Status == "archived", 
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

            var avgMessagesPerSession = totalSessions > 0 ? (double)totalMessages / totalSessions : 0;

            return new SessionStatistics
            {
                TotalSessions = (int)totalSessions,
                ActiveSessions = (int)activeSessions,
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
}

/// <summary>
/// Estadísticas de sesiones
/// </summary>
public class SessionStatistics
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int ArchivedSessions { get; set; }
    public int TotalMessages { get; set; }
    public double AverageMessagesPerSession { get; set; }
    public DateTime? OldestSessionDate { get; set; }
    public DateTime? NewestSessionDate { get; set; }

    public override string ToString()
    {
        return $"Sesiones: {TotalSessions} total, {ActiveSessions} activas, {ArchivedSessions} archivadas, " +
               $"Mensajes: {TotalMessages}, Promedio: {AverageMessagesPerSession:F2} por sesión";
    }
}