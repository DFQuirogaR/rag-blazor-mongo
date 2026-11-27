using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Data;

/// <summary>Configuration limits for persisted chat history operations.</summary>
public sealed class HistoryOptions
{
    public const string SectionName = "History";

    [Range(1, 2000)]
    public int MaxQueryLength { get; set; } = 2000;

    [Range(1, 10000)]
    public int MaxMessagesPerSession { get; set; } = 10000;

    [Range(1, 50)]
    public int DefaultPageSize { get; set; } = 20;

    [Range(0, 120)]
    public int OperationTimeoutSeconds { get; set; } = 0;

    [Range(1, int.MaxValue)]
    public int RetentionDays { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int CleanupIntervalMinutes { get; set; } = 60;
}
