using Lms.Api.Dtos.Assignments;

namespace Lms.Api.Services;

public interface IInstructorAssignmentService
{
    // null = the course does not exist or belongs to another instructor (the controller answers 404).
    Task<List<AssignmentSummaryDto>?> ListAsync(int courseId, int instructorId);
    Task<AssignmentResult<AssignmentDetailDto>> CreateAsync(int courseId, int instructorId, CreateAssignmentRequest request);
    Task<AssignmentResult<AssignmentDetailDto>> GetAsync(int assignmentId, int instructorId);
    Task<AssignmentResult<AssignmentDetailDto>> UpdateAsync(int assignmentId, int instructorId, UpdateAssignmentRequest request);
    Task<AssignmentError> DeleteAsync(int assignmentId, int instructorId);
    Task<AssignmentResult<AssignmentDetailDto>> SetPublishedAsync(int assignmentId, int instructorId, bool publish);
    Task<AssignmentResult<List<SubmissionRowDto>>> ListSubmissionsAsync(int assignmentId, int instructorId);
    Task<AssignmentResult<SubmissionRowDto>> GradeAsync(int submissionId, int instructorId, GradeSubmissionRequest request);
}