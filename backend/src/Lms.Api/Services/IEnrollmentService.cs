using Lms.Api.Dtos.Learning;

namespace Lms.Api.Services;

public enum EnrollError
{
    None,
    CourseNotFound
}

public record EnrollResult(EnrollmentDto? Enrollment, bool Created, EnrollError Error);

public interface IEnrollmentService
{
    Task<EnrollResult> EnrollAsync(int userId, int courseId);
    Task<List<EnrollmentDto>> ListAsync(int userId);
}