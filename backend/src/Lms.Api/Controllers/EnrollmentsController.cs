using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Learning;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = Policies.StudentOnly)]
public class EnrollmentsController(IEnrollmentService enrollments) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    [HttpPost("courses/{courseId:int}/enroll")]
    public async Task<ActionResult<EnrollmentDto>> Enroll(int courseId)
    {
        var result = await enrollments.EnrollAsync(UserId, courseId);
        if (result.Error == EnrollError.CourseNotFound)
            return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);

        // 201 the first time, 200 when the student was already enrolled (idempotent).
        if (result.Created) return StatusCode(StatusCodes.Status201Created, result.Enrollment);
        return Ok(result.Enrollment);
    }

    [HttpGet("me/enrollments")]
    public async Task<ActionResult<List<EnrollmentDto>>> My()
        => Ok(await enrollments.ListAsync(UserId));
}