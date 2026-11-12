using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;

namespace Lms.Api.Services;

public enum CourseActionError
{
    None,
    NotFound,
    NotPublishable,
    PublishedCannotBeDeleted
}

public record CourseActionResult(InstructorCourseDto? Course, CourseActionError Error);

public interface IInstructorCourseService
{
    Task<PagedResult<InstructorCourseDto>> ListAsync(int instructorId, int page, int pageSize);

    // Returns null when the course does not exist OR belongs to another instructor (controller answers 404 for both).
    Task<InstructorCourseDetailDto?> GetAsync(int courseId, int instructorId);

    Task<InstructorCourseDto> CreateAsync(int instructorId, CreateCourseRequest request);

    Task<InstructorCourseDto?> UpdateAsync(int courseId, int instructorId, UpdateCourseRequest request);

    Task<CourseActionResult> PublishAsync(int courseId, int instructorId);

    Task<CourseActionResult> UnpublishAsync(int courseId, int instructorId);

    Task<CourseActionError> DeleteAsync(int courseId, int instructorId);
}