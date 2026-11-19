using System.ComponentModel.DataAnnotations;

namespace Lms.Api.Dtos.Assignments;

public class CreateAssignmentRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(10000, MinimumLength = 2)]
    public string Instructions { get; set; } = string.Empty;

    // Send an ISO date with a zone, e.g. 2026-12-01T21:00:00Z. It is stored as UTC.
    public DateTime? DueDate { get; set; }

    [Range(1, 1000)]
    public int MaxScore { get; set; } = 100;

    public int? ModuleId { get; set; }
}

// Empty on purpose: update and create can get different rules later.
public class UpdateAssignmentRequest : CreateAssignmentRequest
{
}

public class SubmitAssignmentRequest : IValidatableObject
{
    [StringLength(10000)]
    public string? TextContent { get; set; }

    [StringLength(500)]
    public string? LinkUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasText = !string.IsNullOrWhiteSpace(TextContent);
        var hasLink = !string.IsNullOrWhiteSpace(LinkUrl);

        if (!hasText && !hasLink)
            yield return new ValidationResult("Provide a text answer, a link, or both.", new[] { nameof(TextContent), nameof(LinkUrl) });

        if (hasLink &&
            (!Uri.TryCreate(LinkUrl!.Trim(), UriKind.Absolute, out var uri)
             || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            yield return new ValidationResult("LinkUrl must be an http or https link.", new[] { nameof(LinkUrl) });
    }
}

public class GradeSubmissionRequest
{
    // The upper bound (the assignment's MaxScore) is checked by the service.
    [Range(0, 1000)]
    public int Score { get; set; }

    [StringLength(2000)]
    public string? Feedback { get; set; }
}