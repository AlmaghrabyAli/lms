namespace Lms.Api.Services;

public enum AssignmentError
{
    None,
    NotFound,        // 404
    NotEnrolled,     // 403
    NoAccess,        // 403
    NoSubmission,    // 404 the student has not submitted yet
    HasSubmissions,  // 409 cannot delete an assignment that students already submitted
    AlreadyGraded,   // 409 a graded submission can no longer be changed by the student
    ScoreConflict,   // 409 MaxScore cannot go below an already graded score
    InvalidModule,   // 400 the module is not in the assignment's course
    InvalidScore     // 400 the score is above the assignment's MaxScore
}

public record AssignmentResult<T>(T? Value, AssignmentError Error, string? Detail = null) where T : class;