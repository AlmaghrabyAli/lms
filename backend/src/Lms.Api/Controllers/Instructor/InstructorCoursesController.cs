using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers.Instructor;

[ApiController]
[Route("api/instructor/courses")]
[Authorize(Policy = Policies.InstructorOnly)]
public class InstructorCoursesController(IInstructorCourseService courses) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    private ObjectResult CourseNotFound() =>
        Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);

    [HttpGet]
    public async Task<ActionResult<PagedResult<InstructorCourseDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        => Ok(await courses.ListAsync(UserId, page, pageSize));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InstructorCourseDetailDto>> Get(int id)
    {
        var course = await courses.GetAsync(id, UserId);
        if (course is null) return CourseNotFound();
        return Ok(course);
    }

    [HttpPost]
    public async Task<ActionResult<InstructorCourseDto>> Create(CreateCourseRequest request)
    {
        var created = await courses.CreateAsync(UserId, request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InstructorCourseDto>> Update(int id, UpdateCourseRequest request)
    {
        var updated = await courses.UpdateAsync(id, UserId, request);
        if (updated is null) return CourseNotFound();
        return Ok(updated);
    }

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<InstructorCourseDto>> Publish(int id)
    {
        var result = await courses.PublishAsync(id, UserId);
        if (result.Error == CourseActionError.NotFound) return CourseNotFound();
        if (result.Error == CourseActionError.NotPublishable)
            return Problem(
                title: "Course cannot be published yet",
                detail: "Add at least one module with one lesson before publishing.",
                statusCode: StatusCodes.Status409Conflict);
        return Ok(result.Course);
    }

    [HttpPost("{id:int}/unpublish")]
    public async Task<ActionResult<InstructorCourseDto>> Unpublish(int id)
    {
        var result = await courses.UnpublishAsync(id, UserId);
        if (result.Error == CourseActionError.NotFound) return CourseNotFound();
        return Ok(result.Course);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await courses.DeleteAsync(id, UserId);
        if (error == CourseActionError.NotFound) return CourseNotFound();
        if (error == CourseActionError.PublishedCannotBeDeleted)
            return Problem(
                title: "Published course cannot be deleted",
                detail: "Unpublish the course first, then delete it.",
                statusCode: StatusCodes.Status409Conflict);
        return NoContent();
    }
}