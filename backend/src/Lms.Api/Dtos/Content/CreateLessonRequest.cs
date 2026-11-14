using System.ComponentModel.DataAnnotations;
using Lms.Api.Entities;
using Lms.Api.Services;

namespace Lms.Api.Dtos.Content;

public class CreateLessonRequest : IValidatableObject
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    // Nullable + [Required]: a missing "type" becomes 400. The enum arrives as a string ("Video", "Text", "Link").
    [Required]
    public LessonType? Type { get; set; }

    [StringLength(500)]
    public string? VideoUrl { get; set; }

    [StringLength(50000)]
    public string? Content { get; set; }

    [StringLength(500)]
    public string? ExternalUrl { get; set; }

    [Range(1, 1000)]
    public int? DurationMinutes { get; set; }

    // Cross-field rules: what is required depends on the chosen Type.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == LessonType.Video)
        {
            if (string.IsNullOrWhiteSpace(VideoUrl))
                yield return new ValidationResult("VideoUrl is required for a Video lesson.", new[] { nameof(VideoUrl) });
            else if (!YouTubeUrl.TryNormalize(VideoUrl, out _))
                yield return new ValidationResult("Only YouTube links are accepted.", new[] { nameof(VideoUrl) });
        }
        else if (Type == LessonType.Text)
        {
            if (string.IsNullOrWhiteSpace(Content))
                yield return new ValidationResult("Content is required for a Text lesson.", new[] { nameof(Content) });
        }
        else if (Type == LessonType.Link)
        {
            if (string.IsNullOrWhiteSpace(ExternalUrl))
                yield return new ValidationResult("ExternalUrl is required for a Link lesson.", new[] { nameof(ExternalUrl) });
            else if (!Uri.TryCreate(ExternalUrl.Trim(), UriKind.Absolute, out var uri)
                     || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                yield return new ValidationResult("ExternalUrl must be an http or https link.", new[] { nameof(ExternalUrl) });
        }
    }
}