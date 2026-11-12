using System.ComponentModel.DataAnnotations;
using Lms.Api.Entities;

namespace Lms.Api.Dtos.Courses;

public class CreateCourseRequest
{
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Category { get; set; } = string.Empty;

    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    [StringLength(500)]
    [RegularExpression(@"^https?://\S+$", ErrorMessage = "ThumbnailUrl must be an http or https URL.")]
    public string? ThumbnailUrl { get; set; }
}