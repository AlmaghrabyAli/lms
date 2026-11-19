using Lms.Api.Entities;

namespace Lms.Api.Dtos.Quizzes;

// ---------- instructor view (contains IsCorrect) ----------
public record OptionDto(int Id, string Text, bool IsCorrect);

public record QuestionDto(int Id, string Text, QuestionType Type, int Points, int OrderIndex, List<OptionDto> Options);

public record QuizSummaryDto(
    int Id, string Title, string? Description, int? ModuleId, string? ModuleTitle,
    int PassingScorePercent, int? MaxAttempts, bool IsPublished, int QuestionCount, int TotalPoints, int AttemptCount);

public record QuizDetailDto(
    int Id, int CourseId, int? ModuleId, string Title, string? Description,
    int PassingScorePercent, int? MaxAttempts, bool IsPublished, bool IsLocked, int AttemptCount,
    int TotalPoints, List<QuestionDto> Questions);

public record QuizStudentResultDto(
    int UserId, string FullName, string Email, int AttemptsCount, int? BestScorePercent, bool Passed, DateTime? LastSubmittedAt);

// ---------- student view: these types do not even HAVE an IsCorrect field ----------
public record StudentOptionDto(int Id, string Text);

public record StudentQuestionDto(int Id, string Text, QuestionType Type, int Points, int OrderIndex, List<StudentOptionDto> Options);

public record QuizListItemDto(
    int Id, int CourseId, string Title, string? Description, int? ModuleId,
    int QuestionCount, int TotalPoints, int PassingScorePercent, int? MaxAttempts,
    int AttemptsUsed, int? AttemptsLeft, int? BestScorePercent, bool Passed, int? InProgressAttemptId);

public record AttemptStartDto(
    int AttemptId, int QuizId, string Title, int PassingScorePercent, int TotalPoints,
    DateTime StartedAt, bool Resumed, List<StudentQuestionDto> Questions);

public record AttemptSummaryDto(int Id, DateTime StartedAt, DateTime? SubmittedAt, int? ScorePercent, bool? Passed);

public record MyAttemptsDto(
    int QuizId, int AttemptsUsed, int? MaxAttempts, int? AttemptsLeft, int? BestScorePercent, List<AttemptSummaryDto> Attempts);

// ---------- after submit: correct answers are revealed ----------
public record ResultOptionDto(int Id, string Text, bool IsCorrect, bool WasSelected);

public record ResultQuestionDto(
    int Id, string Text, QuestionType Type, int Points, int OrderIndex, bool IsCorrect, int EarnedPoints, List<ResultOptionDto> Options);

public record AttemptResultDto(
    int AttemptId, int QuizId, string QuizTitle, int CourseId, DateTime StartedAt, DateTime SubmittedAt,
    int ScorePercent, bool Passed, int PassingScorePercent, int EarnedPoints, int TotalPoints,
    List<ResultQuestionDto> Questions);