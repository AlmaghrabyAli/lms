namespace Lms.Api.Dtos.Learning;

public record ContinueLearningDto(
    int CourseId,
    string CourseTitle,
    string Slug,
    int NextLessonId,
    string NextLessonTitle,
    int ProgressPercent);

// Placeholders: these two lists stay empty until Phase 7 (assignments) and Phase 6 (quizzes),
// so the frontend contract is stable from today. The fields can still change when those phases arrive.
public record UpcomingAssignmentDto(int AssignmentId, int CourseId, string CourseTitle, string Title, DateTime? DueDate);

public record RecentQuizResultDto(
    int AttemptId, int QuizId, string QuizTitle, int CourseId, string CourseTitle,
    int ScorePercent, bool Passed, DateTime SubmittedAt);

public record DashboardDto(
    int EnrolledCount,
    int CompletedCount,
    int InProgressCount,
    int CompletedLessonsCount,
    ContinueLearningDto? ContinueLearning,
    List<EnrollmentDto> Enrollments,
    List<UpcomingAssignmentDto> UpcomingAssignments,
    List<RecentQuizResultDto> RecentQuizResults);