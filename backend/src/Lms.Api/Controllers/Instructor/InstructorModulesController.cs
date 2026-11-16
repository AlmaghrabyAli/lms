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
public class InstructorModulesController(IInstructorContentService content) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    private ObjectResult CourseNotFound() =>
        Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult ModuleNotFound() =>
        Problem(title: "Module not found", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult LastLessonConflict() =>
        Problem(
            title: "Module cannot be deleted",
            detail: "A published course must keep at least one lesson. Unpublish it first.",
            statusCode: StatusCodes.Status409Conflict);

    private ObjectResult HasProgressConflict() =>
        Problem(
            title: "Module cannot be deleted",
            detail: "Students already have progress on lessons of this module; unpublish or edit it instead.",
            statusCode: StatusCodes.Status409Conflict);

    [HttpPost("courses/{courseId:int}/modules")]
    public async Task<ActionResult<ModuleDto>> Create(int courseId, CreateModuleRequest request)
    {
        var result = await content.CreateModuleAsync(courseId, UserId, request);
        if (result.Error == ContentActionError.NotFound) return CourseNotFound();
        // There is no GET for a single module; modules are read through the course outline.
        return Created($"/api/instructor/courses/{courseId}", result.Module);
    }

    [HttpPut("modules/{moduleId:int}")]
    public async Task<ActionResult<ModuleDto>> Update(int moduleId, UpdateModuleRequest request)
    {
        var result = await content.UpdateModuleAsync(moduleId, UserId, request);
        if (result.Error == ContentActionError.NotFound) return ModuleNotFound();
        return Ok(result.Module);
    }

    [HttpDelete("modules/{moduleId:int}")]
    public async Task<IActionResult> Delete(int moduleId)
    {
        var error = await content.DeleteModuleAsync(moduleId, UserId);
        if (error == ContentActionError.NotFound) return ModuleNotFound();
        if (error == ContentActionError.HasStudentProgress) return HasProgressConflict();
        if (error == ContentActionError.LastLessonOfPublishedCourse) return LastLessonConflict();
        return NoContent();
    }

    [HttpPost("modules/{moduleId:int}/move")]
    public async Task<ActionResult<List<ModuleDto>>> Move(int moduleId, MoveRequest request)
    {
        var result = await content.MoveModuleAsync(moduleId, UserId, request.Direction!.Value);
        if (result.Error == ContentActionError.NotFound) return ModuleNotFound();
        return Ok(result.Modules);
    }
}