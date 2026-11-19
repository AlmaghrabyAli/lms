using Lms.Api.Authorization;
using Lms.Api.Dtos.Assignments;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api")]
public class AssignmentsController(IAssignmentTakingService assignments) : AssignmentControllerBase
{
    // Read endpoints: any logged-in role may call them, the service decides (enrolled student / owner / admin preview).
    [Authorize]
    [HttpGet("courses/{courseId:int}/assignments")]
    public async Task<ActionResult<List<AssignmentListItemDto>>> ListForCourse(int courseId)
    {
        var result = await assignments.ListForCourseAsync(courseId, UserId, Role);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Course not found");
        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("assignments/{assignmentId:int}")]
    public async Task<ActionResult<StudentAssignmentDto>> Get(int assignmentId)
    {
        var result = await assignments.GetAsync(assignmentId, UserId, Role);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpPost("assignments/{assignmentId:int}/submission")]
    public async Task<ActionResult<SubmissionDto>> Submit(int assignmentId, [FromBody] SubmitAssignmentRequest request)
    {
        var result = await assignments.SubmitAsync(assignmentId, UserId, request);
        if (result.Error != AssignmentError.None) return MapError(result.Error, null, "Assignment not found");
        if (result.Created) return StatusCode(StatusCodes.Status201Created, result.Submission);
        return Ok(result.Submission);
    }

    [Authorize(Policy = Policies.StudentOnly)]
    [HttpGet("assignments/{assignmentId:int}/my-submission")]
    public async Task<ActionResult<SubmissionDto>> MySubmission(int assignmentId)
    {
        var result = await assignments.MySubmissionAsync(assignmentId, UserId);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }
}