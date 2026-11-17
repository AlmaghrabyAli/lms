using Lms.Api.Entities;

namespace Lms.Api.Dtos.Learning;

// The outline has NO lesson content, video url or external url: the player loads a lesson lazily.
public record LearnLessonDto(int Id, string Title, LessonType Type, int? DurationMinutes, int OrderIndex, bool IsCompleted);

public record LearnModuleDto(int Id, string Title, int OrderIndex, List<LearnLessonDto> Lessons);

public record LearnCourseDto(
    int CourseId,
    string Title,
    string Slug,
    int ProgressPercent,
    int CompletedLessons,
    int TotalLessons,
    DateTime? EnrollmentCompletedAt,
    List<LearnModuleDto> Modules);