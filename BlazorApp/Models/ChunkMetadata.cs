using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Contains metadata about a specific chunk of text extracted from a document.
/// </summary>
public class ChunkMetadata
{
    /// <summary>
    /// Type of content (e.g., text, code, table, list).
    /// </summary>
    [StringLength(50, ErrorMessage = "ContentType cannot exceed 50 characters.")]
    public string? ContentType { get; set; }

    /// <summary>
    /// Heading level for titles (1-6 for HTML-style headings).
    /// Null for non-heading content.
    /// </summary>
    [Range(0, 6, ErrorMessage = "HeadingLevel must be between 0 and 6.")]
    public int? HeadingLevel { get; set; }

    /// <summary>
    /// Indicates if this chunk is a list item.
    /// </summary>
    public bool IsListItem { get; set; }

    /// <summary>
    /// Number of the list item (for ordered lists).
    /// Null for unordered lists.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "ListItemNumber must be a positive number.")]
    public int? ListItemNumber { get; set; }

    /// <summary>
    /// Indicates if this chunk contains code.
    /// </summary>
    public bool HasCode { get; set; }

    /// <summary>
    /// Programming language if this is a code block.
    /// </summary>
    [StringLength(50, ErrorMessage = "Language cannot exceed 50 characters.")]
    public string? Language { get; set; }

    /// <summary>
    /// Named entities detected in this chunk (people, organizations, locations).
    /// </summary>
    public List<string> NamedEntities { get; set; } = new();

    /// <summary>
    /// Topics or keywords extracted from this chunk.
    /// </summary>
    public List<string> Topics { get; set; } = new();

    /// <summary>
    /// Optional section title where this chunk appears.
    /// </summary>
    [StringLength(500, ErrorMessage = "SectionTitle cannot exceed 500 characters.")]
    public string? SectionTitle { get; set; }

    /// <summary>
    /// Optional section number (e.g., "3.2.1").
    /// </summary>
    [StringLength(50, ErrorMessage = "SectionNumber cannot exceed 50 characters.")]
    public string? SectionNumber { get; set; }

    /// <summary>
    /// Page number detected while chunking (e.g., from a "--- Page N ---" marker).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be a positive number.")]
    public int? PageNumber { get; set; }
}
