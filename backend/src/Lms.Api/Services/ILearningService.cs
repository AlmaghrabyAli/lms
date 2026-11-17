using Lms.Api.Dtos.Learning;

namespace Lms.Api.Services;

public enum LearningError
{
    None,
    NotFound,     // course/lesson does not exist or the course is a draft -> 404
    NotEnrolled,  // student is not enrolled -> 403
    NoAccess      // instructor who does not own the course (or unknown role) -> 403
}

public record LearnResult(LearnCourseDto? Course, LearningError Error);

public record LessonViewResult(LessonViewDto? Lesson, LearningError Error);

public record ProgressResult(ProgressDto? Progress, LearningError Error);

public interface ILearningService
{
    Task<LearnResult> GetLearnAsync(int courseId, int userId, string role);
    Task<LessonViewResult> GetLessonAsync(int lessonId, int userId, string role);
    Task<ProgressResult> CompleteAsync(int lessonId, int userId);
    Task<ProgressResult> UncompleteAsync(int lessonId, int userId);
}