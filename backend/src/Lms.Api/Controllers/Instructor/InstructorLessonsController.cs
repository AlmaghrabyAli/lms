using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Content;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers.Instructor;

[ApiController]
[Route("api/instructor")]
[Authorize(Policy = Policies.InstructorOnly)]
public class InstructorLessonsController(IInstructorContentService content) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    private ObjectResult ModuleNotFound() =>
        Problem(title: "Module not found", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult LessonNotFound() =>
        Problem(title: "Lesson not found", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult LastLessonConflict() =>
        Problem(
            title: "Lesson cannot be deleted",
            detail: "A published course must keep at least one lesson. Unpublish it first.",
            statusCode: StatusCodes.Status409Conflict);

    private ObjectResult HasProgressConflict() =>
        Problem(
            title: "Lesson cannot be deleted",
            detail: "Students already have progress on this lesson; unpublish or edit it instead.",
            statusCode: StatusCodes.Status409Conflict);

    [HttpPost("modules/{moduleId:int}/lessons")]
    public async Task<ActionResult<LessonDto>> Create(int moduleId, CreateLessonRequest request)
    {
        var result = await content.CreateLessonAsync(moduleId, UserId, request);
        if (result.Error == ContentActionError.NotFound) return ModuleNotFound();
        // There is no GET for a single lesson on this owner-only API (the course outline has them all).
        return StatusCode(StatusCodes.Status201Created, result.Lesson);
    }

    [HttpPut("lessons/{lessonId:int}")]
    public async Task<ActionResult<LessonDto>> Update(int lessonId, UpdateLessonRequest request)
    {
        var result = await content.UpdateLessonAsync(lessonId, UserId, request);
        if (result.Error == ContentActionError.NotFound) return LessonNotFound();
        return Ok(result.Lesson);
    }

    [HttpDelete("lessons/{lessonId:int}")]
    public async Task<IActionResult> Delete(int lessonId)
    {
        var error = await content.DeleteLessonAsync(lessonId, UserId);
        if (error == ContentActionError.NotFound) return LessonNotFound();
        if (error == ContentActionError.HasStudentProgress) return HasProgressConflict();
        if (error == ContentActionError.LastLessonOfPublishedCourse) return LastLessonConflict();
        return NoContent();
    }

    [HttpPost("lessons/{lessonId:int}/move")]
    public async Task<ActionResult<List<LessonDto>>> Move(int lessonId, MoveRequest request)
    {
        var result = await content.MoveLessonAsync(lessonId, UserId, request.Direction!.Value);
        if (result.Error == ContentActionError.NotFound) return LessonNotFound();
        return Ok(result.Lessons);
    }
}