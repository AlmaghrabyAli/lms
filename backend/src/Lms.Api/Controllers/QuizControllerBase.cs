using System.Security.Claims;
using Lms.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

// Shared by the instructor and the student quiz controllers: the user id/role from the JWT and ONE error mapping.
public abstract class QuizControllerBase : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue("sub")!);
    protected string Role => User.FindFirstValue("role") ?? string.Empty;

    protected ObjectResult MapError(QuizError error, string? detail, string notFoundTitle) => error switch
    {
        QuizError.NotFound => Problem(title: notFoundTitle, statusCode: StatusCodes.Status404NotFound),
        QuizError.NotEnrolled => Problem(title: "You are not enrolled in this course", statusCode: StatusCodes.Status403Forbidden),
        QuizError.NoAccess => Problem(title: "You do not have access to this course", statusCode: StatusCodes.Status403Forbidden),
        QuizError.HasAttempts => Problem(title: "Quiz is locked",
            detail: detail ?? "Students have already attempted this quiz.", statusCode: StatusCodes.Status409Conflict),
        QuizError.NoAttemptsLeft => Problem(title: "No attempts left",
            detail: "You have used all the attempts allowed for this quiz.", statusCode: StatusCodes.Status409Conflict),
        QuizError.AlreadySubmitted => Problem(title: "Attempt already submitted",
            detail: "A submitted attempt cannot be changed.", statusCode: StatusCodes.Status409Conflict),
        QuizError.NotSubmitted => Problem(title: "Attempt not submitted yet",
            detail: "Results are available after you submit the attempt.", statusCode: StatusCodes.Status409Conflict),
        QuizError.NoQuestions => Problem(title: "Quiz cannot be published",
            detail: "Add at least one question first.", statusCode: StatusCodes.Status409Conflict),
        QuizError.LastQuestionOfPublishedQuiz => Problem(title: "Question cannot be deleted",
            detail: "A published quiz must keep at least one question; unpublish it first.", statusCode: StatusCodes.Status409Conflict),
        QuizError.InvalidModule => Problem(title: "Invalid module",
            detail: "The module must belong to the same course.", statusCode: StatusCodes.Status400BadRequest),
        QuizError.InvalidAnswers => Problem(title: "Invalid answers",
            detail: detail ?? "The submitted answers do not match this quiz.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(title: "Unexpected error", statusCode: StatusCodes.Status500InternalServerError)
    };
}