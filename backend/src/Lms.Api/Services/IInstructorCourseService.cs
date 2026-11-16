using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;

namespace Lms.Api.Services;

public enum CourseActionError
{
    None,
    NotFound,
    NotPublishable,
    PublishedCannotBeDeleted,
    HasEnrollments
}

public record CourseActionResult(InstructorCourseDto? Course, CourseActionError Error);

public interface IInstructorCourseService
{
    Task<PagedResult<InstructorCourseDto>> ListAsync(int instructorId, int page, int pageSize);
    Task<InstructorCourseDetailDto?> GetAsync(int courseId, int instructorId);
    Task<InstructorCourseDto> CreateAsync(int instructorId, CreateCourseRequest request);
    Task<InstructorCourseDto?> UpdateAsync(int courseId, int instructorId, UpdateCourseRequest request);
    Task<CourseActionResult> PublishAsync(int courseId, int instructorId);
    Task<CourseActionResult> UnpublishAsync(int courseId, int instructorId);
    Task<CourseActionError> DeleteAsync(int courseId, int instructorId);
}