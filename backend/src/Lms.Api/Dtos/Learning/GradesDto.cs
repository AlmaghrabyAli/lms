namespace Lms.Api.Dtos.Learning;

public record AssignmentGradeDto(
    int SubmissionId, int AssignmentId, int CourseId, string CourseTitle, string AssignmentTitle,
    int MaxScore, int? Score, string? Feedback, DateTime SubmittedAt, bool IsLate, DateTime? GradedAt);

public record QuizGradeDto(
    int QuizId, int CourseId, string CourseTitle, string QuizTitle,
    int AttemptsUsed, int? BestScorePercent, bool Passed, DateTime? LastSubmittedAt);

public record GradesDto(List<AssignmentGradeDto> Assignments, List<QuizGradeDto> Quizzes);