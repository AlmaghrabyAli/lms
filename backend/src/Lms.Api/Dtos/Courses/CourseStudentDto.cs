namespace Lms.Api.Dtos.Courses;

// One row of the instructor's "students" table. Progress is computed live from the lesson progress rows.
public record CourseStudentDto(
    int UserId,
    string FullName,
    string Email,
    DateTime EnrolledAt,
    DateTime? LastActivityAt,
    int CompletedLessons,
    int TotalLessons,
    int ProgressPercent,
    bool IsCompleted);