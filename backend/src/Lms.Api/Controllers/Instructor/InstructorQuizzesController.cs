using Lms.Api.Authorization;
using Lms.Api.Dtos.Quizzes;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers.Instructor;

[ApiController]
[Route("api/instructor")]
[Authorize(Policy = Policies.InstructorOnly)]
public class InstructorQuizzesController(IInstructorQuizService quizzes) : QuizControllerBase
{
    [HttpGet("courses/{courseId:int}/quizzes")]
    public async Task<ActionResult<List<QuizSummaryDto>>> List(int courseId)
    {
        var result = await quizzes.ListAsync(courseId, UserId);
        if (result is null) return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);
        return Ok(result);
    }

    [HttpPost("courses/{courseId:int}/quizzes")]
    public async Task<ActionResult<QuizDetailDto>> Create(int courseId, [FromBody] CreateQuizRequest request)
    {
        var result = await quizzes.CreateAsync(courseId, UserId, request);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Course not found");
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("quizzes/{quizId:int}")]
    public async Task<ActionResult<QuizDetailDto>> Get(int quizId)
    {
        var result = await quizzes.GetAsync(quizId, UserId);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [HttpPut("quizzes/{quizId:int}")]
    public async Task<ActionResult<QuizDetailDto>> Update(int quizId, [FromBody] UpdateQuizRequest request)
    {
        var result = await quizzes.UpdateAsync(quizId, UserId, request);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [HttpDelete("quizzes/{quizId:int}")]
    public async Task<IActionResult> Delete(int quizId)
    {
        var error = await quizzes.DeleteAsync(quizId, UserId);
        if (error != QuizError.None)
            return MapError(error, error == QuizError.HasAttempts ? "Students have already attempted this quiz; unpublish it instead." : null, "Quiz not found");
        return NoContent();
    }

    [HttpPost("quizzes/{quizId:int}/publish")]
    public async Task<ActionResult<QuizDetailDto>> Publish(int quizId)
    {
        var result = await quizzes.SetPublishedAsync(quizId, UserId, true);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [HttpPost("quizzes/{quizId:int}/unpublish")]
    public async Task<ActionResult<QuizDetailDto>> Unpublish(int quizId)
    {
        var result = await quizzes.SetPublishedAsync(quizId, UserId, false);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [HttpGet("quizzes/{quizId:int}/results")]
    public async Task<ActionResult<List<QuizStudentResultDto>>> Results(int quizId)
    {
        var result = await quizzes.ListResultsAsync(quizId, UserId);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return Ok(result.Value);
    }

    [HttpPost("quizzes/{quizId:int}/questions")]
    public async Task<ActionResult<QuestionDto>> AddQuestion(int quizId, [FromBody] SaveQuestionRequest request)
    {
        var result = await quizzes.AddQuestionAsync(quizId, UserId, request);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Quiz not found");
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("questions/{questionId:int}")]
    public async Task<ActionResult<QuestionDto>> UpdateQuestion(int questionId, [FromBody] SaveQuestionRequest request)
    {
        var result = await quizzes.UpdateQuestionAsync(questionId, UserId, request);
        if (result.Error != QuizError.None) return MapError(result.Error, result.Detail, "Question not found");
        return Ok(result.Value);
    }

    [HttpDelete("questions/{questionId:int}")]
    public async Task<IActionResult> DeleteQuestion(int questionId)
    {
        var error = await quizzes.DeleteQuestionAsync(questionId, UserId);
        if (error != QuizError.None) return MapError(error, null, "Question not found");
        return NoContent();
    }
}