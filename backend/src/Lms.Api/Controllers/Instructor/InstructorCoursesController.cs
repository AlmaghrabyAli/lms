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

    [HttpGet]
    public async Task<ActionResult<PagedResult<InstructorCourseDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        => Ok(await courses.ListAsync(UserId, page, pageSize));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InstructorCourseDetailDto>> Get(int id)
    {
        var course = await courses.GetAsync(id, UserId);
        if (course is null) return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);
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
        if (updated is null) return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);
        return Ok(updated);
    }
}