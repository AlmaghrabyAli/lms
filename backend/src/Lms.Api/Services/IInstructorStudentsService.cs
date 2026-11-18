using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;

namespace Lms.Api.Services;

public interface IInstructorStudentsService
{
    // null = the course does not exist or belongs to another instructor (the controller answers 404).
    Task<PagedResult<CourseStudentDto>?> ListAsync(int courseId, int instructorId, int page, int pageSize);
}