using Lms.Api.Dtos.Assignments;

namespace Lms.Api.Services;

// Created = true when this was the student's first submission (the controller answers 201, otherwise 200).
public record SubmitResult(SubmissionDto? Submission, AssignmentError Error, bool Created);

public interface IAssignmentTakingService
{
    Task<AssignmentResult<List<AssignmentListItemDto>>> ListForCourseAsync(int courseId, int userId, string role);
    Task<AssignmentResult<StudentAssignmentDto>> GetAsync(int assignmentId, int userId, string role);
    Task<SubmitResult> SubmitAsync(int assignmentId, int userId, SubmitAssignmentRequest request);
    Task<AssignmentResult<SubmissionDto>> MySubmissionAsync(int assignmentId, int userId);
}