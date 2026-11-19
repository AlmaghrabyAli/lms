namespace Lms.Api.Dtos.Assignments;

public enum SubmissionStatus { NotSubmitted, Submitted, Graded }

// ---------- instructor view ----------
public record AssignmentSummaryDto(
    int Id, string Title, int? ModuleId, string? ModuleTitle, DateTime? DueDate, int MaxScore,
    bool IsPublished, int SubmissionCount, int GradedCount);

public record AssignmentDetailDto(
    int Id, int CourseId, int? ModuleId, string Title, string Instructions, DateTime? DueDate, int MaxScore,
    bool IsPublished, int SubmissionCount, int GradedCount);

// One row of the grading table.
public record SubmissionRowDto(
    int SubmissionId, int StudentId, string FullName, string Email, string? TextContent, string? LinkUrl,
    DateTime SubmittedAt, bool IsLate, int? Score, string? Feedback, DateTime? GradedAt);

// ---------- student view ----------
public record SubmissionDto(
    int Id, string? TextContent, string? LinkUrl, DateTime SubmittedAt, bool IsLate,
    int? Score, int MaxScore, string? Feedback, DateTime? GradedAt);

public record AssignmentListItemDto(
    int Id, int CourseId, string Title, int? ModuleId, DateTime? DueDate, int MaxScore,
    SubmissionStatus Status, int? Score, DateTime? SubmittedAt, bool IsLate);

public record StudentAssignmentDto(
    int Id, int CourseId, string Title, string Instructions, int? ModuleId, DateTime? DueDate, int MaxScore,
    SubmissionStatus Status, SubmissionDto? MySubmission);