using Lms.Api.Authorization;
using Lms.Api.Dtos.Quizzes;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api")]
public class QuizzesController(IQuizTakingService quizzes) : QuizControllerBase
{
    // Read endpoints: any logged-in role may call them, the service decides (enrolled student / owner / admin preview).
    [Authorize]
    [HttpGet("courses/{courseId:int}/quizzes")]
    public async Task<ActionResult<List<QuizListItemDto>>> ListForCourse(int courseId)
    {
        var result = await quizzes.ListForCourseAsync(courseId, UserId, Role);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Course not found");
        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("quizzes/{quizId:int}")]
    public async Task<ActionResult<QuizListItemDto>> Get(int quizId)
    {
        var result = await quizzes.GetAsync(quizId, UserId, Role);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpPost("quizzes/{quizId:int}/attempts")]
    public async Task<ActionResult<AttemptStartDto>> Start(int quizId)
    {
        var result = await quizzes.StartAttemptAsync(quizId, UserId);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        if (result.Value!.Resumed) return Ok(result.Value);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpGet("quizzes/{quizId:int}/my-attempts")]
    public async Task<ActionResult<MyAttemptsDto>> MyAttempts(int quizId)
    {
        var result = await quizzes.MyAttemptsAsync(quizId, UserId);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpPost("attempts/{attemptId:int}/submit")]
    public async Task<ActionResult<AttemptResultDto>> Submit(int attemptId, [FromBody] SubmitAttemptRequest request)
    {
        var result = await quizzes.SubmitAsync(attemptId, UserId, request);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Attempt not found");
        return Ok(result.Value);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpGet("attempts/{attemptId:int}")]
    public async Task<ActionResult<AttemptResultDto>> Result(int attemptId)
    {
        var result = await quizzes.GetResultAsync(attemptId, UserId);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Attempt not found");
        return Ok(result.Value);
    }
}