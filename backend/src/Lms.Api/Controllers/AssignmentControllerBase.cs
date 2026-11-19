using System.Security.Claims;
using Lms.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

// Shared by the instructor and the student assignment controllers: the user id/role from the JWT and ONE error mapping.
public abstract class AssignmentControllerBase : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue("sub")!);
    protected string Role => User.FindFirstValue("role") ?? string.Empty;

    protected ObjectResult MapError(AssignmentError error, string? detail, string notFoundTitle) => error switch
    {
        AssignmentError.NotFound => Problem(title: notFoundTitle, statusCode: StatusCodes.Status404NotFound),
        AssignmentError.NotEnrolled => Problem(title: "You are not enrolled in this course", statusCode: StatusCodes.Status403Forbidden),
        AssignmentError.NoAccess => Problem(title: "You do not have access to this course", statusCode: StatusCodes.Status403Forbidden),
        AssignmentError.NoSubmission => Problem(title: "No submission yet",
            detail: "You have not submitted this assignment.", statusCode: StatusCodes.Status404NotFound),
        AssignmentError.HasSubmissions => Problem(title: "Assignment cannot be deleted",
            detail: "Students have already submitted work; unpublish it instead.", statusCode: StatusCodes.Status409Conflict),
        AssignmentError.AlreadyGraded => Problem(title: "Submission already graded",
            detail: "A graded submission can no longer be changed.", statusCode: StatusCodes.Status409Conflict),
        AssignmentError.ScoreConflict => Problem(title: "MaxScore conflict",
            detail: detail ?? "A graded score is higher than the new MaxScore.", statusCode: StatusCodes.Status409Conflict),
        AssignmentError.InvalidModule => Problem(title: "Invalid module",
            detail: "The module must belong to the same course.", statusCode: StatusCodes.Status400BadRequest),
        AssignmentError.InvalidScore => Problem(title: "Invalid score",
            detail: detail ?? "The score is above the assignment's MaxScore.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(title: "Unexpected error", statusCode: StatusCodes.Status500InternalServerError)
    };
}