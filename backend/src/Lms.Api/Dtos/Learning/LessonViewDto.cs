using Lms.Api.Entities;

namespace Lms.Api.Dtos.Learning;

public record LessonViewDto(
    int Id,
    int CourseId,
    int ModuleId,
    string Title,
    LessonType Type,
    string? VideoUrl,
    string? Content,
    string? ExternalUrl,
    int? DurationMinutes,
    int OrderIndex,
    bool IsCompleted,
    int? PreviousLessonId,
    int? NextLessonId);