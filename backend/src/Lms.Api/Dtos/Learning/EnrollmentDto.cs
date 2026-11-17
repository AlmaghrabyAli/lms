using Lms.Api.Entities;

namespace Lms.Api.Dtos.Learning;

public record EnrollmentDto(
    int Id,
    int CourseId,
    string Title,
    string Slug,
    string? ThumbnailUrl,
    CourseLevel Level,
    string Category,
    string InstructorName,
    DateTime EnrolledAt,
    DateTime? CompletedAt,
    int CompletedLessons,
    int TotalLessons,
    int ProgressPercent,
    bool IsAvailable);