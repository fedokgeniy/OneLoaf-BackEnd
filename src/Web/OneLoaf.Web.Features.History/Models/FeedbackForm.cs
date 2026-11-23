using System.ComponentModel.DataAnnotations;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.History.Models;

/// <summary>Rating and comment of a bake (US-32); empty values clear them.</summary>
public sealed class FeedbackForm
{
    /// <summary>Rating 1–5 or none.</summary>
    [Range(1, 5, ErrorMessage = "Choose 1 to 5.")]
    public int? Rating { get; set; }

    /// <summary>What went well or wrong.</summary>
    [StringLength(4000, ErrorMessage = "Use at most 4000 characters.")]
    public string? Comment { get; set; }

    /// <summary>Form filled from a record.</summary>
    public static FeedbackForm From(BakeRecordDto record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new() { Rating = record.Rating, Comment = record.Comment };
    }

    /// <summary>PATCH body; a blank comment is sent as null.</summary>
    public UpdateBakeRecordRequest ToRequest() => new(Rating, string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim());
}
