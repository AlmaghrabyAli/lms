using Lms.Api.Authorization;
using Lms.Api.Dtos.Assignments;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers.Instructor;

[ApiController]
[Route("api/instructor")]
[Authorize(Policy = Policies.InstructorOnly)]
public class InstructorAssignmentsController(IInstructorAssignmentService assignments) : AssignmentControllerBase
{
    [HttpGet("courses/{courseId:int}/assignments")]
    public async Task<ActionResult<List<AssignmentSummaryDto>>> List(int courseId)
    {
        var result = await assignments.ListAsync(courseId, UserId);
        if (result is null) return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);
        return Ok(result);
    }

    [HttpPost("courses/{courseId:int}/assignments")]
    public async Task<ActionResult<AssignmentDetailDto>> Create(int courseId, [FromBody] CreateAssignmentRequest request)
    {
        var result = await assignments.CreateAsync(courseId, UserId, request);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Course not found");
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("assignments/{assignmentId:int}")]
    public async Task<ActionResult<AssignmentDetailDto>> Get(int assignmentId)
    {
        var result = await assignments.GetAsync(assignmentId, UserId);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }

    [HttpPut("assignments/{assignmentId:int}")]
    public async Task<ActionResult<AssignmentDetailDto>> Update(int assignmentId, [FromBody] UpdateAssignmentRequest request)
    {
        var result = await assignments.UpdateAsync(assignmentId, UserId, request);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }

    [HttpDelete("assignments/{assignmentId:int}")]
    public async Task<IActionResult> Delete(int assignmentId)
    {
        var error = await assignments.DeleteAsync(assignmentId, UserId);
        if (error != AssignmentError.None) return MapError(error, null, "Assignment not found");
        return NoContent();
    }

    [HttpPost("assignments/{assignmentId:int}/publish")]
    public async Task<ActionResult<AssignmentDetailDto>> Publish(int assignmentId)
    {
        var result = await assignments.SetPublishedAsync(assignmentId, UserId, true);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }

    [HttpPost("assignments/{assignmentId:int}/unpublish")]
    public async Task<ActionResult<AssignmentDetailDto>> Unpublish(int assignmentId)
    {
        var result = await assignments.SetPublishedAsync(assignmentId, UserId, false);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }

    [HttpGet("assignments/{assignmentId:int}/submissions")]
    public async Task<ActionResult<List<SubmissionRowDto>>> Submissions(int assignmentId)
    {
        var result = await assignments.ListSubmissionsAsync(assignmentId, UserId);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Assignment not found");
        return Ok(result.Value);
    }

    [HttpPut("submissions/{submissionId:int}/grade")]
    public async Task<ActionResult<SubmissionRowDto>> Grade(int submissionId, [FromBody] GradeSubmissionRequest request)
    {
        var result = await assignments.GradeAsync(submissionId, UserId, request);
        if (result.Error != AssignmentError.None) return MapError(result.Error, result.Detail, "Submission not found");
        return Ok(result.Value);
    }
}