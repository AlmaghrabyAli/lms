namespace Lms.Api.Dtos.Learning;

public record ProgressDto(
    int CourseId,
    int LessonId,
    bool IsLessonCompleted,
    int CompletedLessons,
    int TotalLessons,
    int ProgressPercent,
    bool CourseCompleted,
    DateTime? EnrollmentCompletedAt);