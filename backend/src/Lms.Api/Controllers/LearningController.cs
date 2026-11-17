using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Learning;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api")]
public class LearningController(ILearningService learning) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    private string Role => User.FindFirstValue("role") ?? string.Empty;

    private ObjectResult MapError(LearningError error, string notFoundTitle) => error switch
    {
        LearningError.NotFound => Problem(title: notFoundTitle, statusCode: StatusCodes.Status404NotFound),
        LearningError.NotEnrolled => Problem(title: "You are not enrolled in this course", statusCode: StatusCodes.Status403Forbidden),
        _ => Problem(title: "You do not have access to this course", statusCode: StatusCodes.Status403Forbidden)
    };

    // Any logged-in role may call the read endpoints: the service decides (enrolled student / owner / admin).
    [Authorize]
    [HttpGet("courses/{courseId:int}/learn")]
    public async Task<ActionResult<LearnCourseDto>> Learn(int courseId)
    {
        var result = await learning.GetLearnAsync(courseId, UserId, Role);
        if (result.Error != LearningError.None) return MapError(result.Error, "Course not found");
        return Ok(result.Course);
    }

    [Authorize]
    [HttpGet("lessons/{lessonId:int}")]
    public async Task<ActionResult<LessonViewDto>> Lesson(int lessonId)
    {
        var result = await learning.GetLessonAsync(lessonId, UserId, Role);
        if (result.Error != LearningError.None) return MapError(result.Error, "Lesson not found");
        return Ok(result.Lesson);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpPost("lessons/{lessonId:int}/complete")]
    public async Task<ActionResult<ProgressDto>> Complete(int lessonId)
    {
        var result = await learning.CompleteAsync(lessonId, UserId);
        if (result.Error != LearningError.None) return MapError(result.Error, "Lesson not found");
        return Ok(result.Progress);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpDelete("lessons/{lessonId:int}/complete")]
    public async Task<ActionResult<ProgressDto>> Uncomplete(int lessonId)
    {
        var result = await learning.UncompleteAsync(lessonId, UserId);
        if (result.Error != LearningError.None) return MapError(result.Error, "Lesson not found");
        return Ok(result.Progress);
    }
}