using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers.Instructor;

[ApiController]
[Route("api/instructor/courses/{courseId:int}/students")]
[Authorize(Policy = Policies.InstructorOnly)]
public class InstructorStudentsController(IInstructorStudentsService students) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseStudentDto>>> List(int courseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await students.ListAsync(courseId, UserId, page, pageSize);
        if (result is null) return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);
        return Ok(result);
    }
}