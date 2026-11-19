using Lms.Api.Dtos.Admin;
using Lms.Api.Dtos.Common;

namespace Lms.Api.Services;

public interface IAdminService
{
    Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, string? role, bool? isActive, int page, int pageSize);
    Task<AdminResult<AdminUserDto>> ChangeRoleAsync(int callerId, int userId, string role);
    Task<AdminResult<AdminUserDto>> SetActiveAsync(int callerId, int userId, bool isActive);
    Task<PagedResult<AdminCourseDto>> ListCoursesAsync(string? search, bool? published, int page, int pageSize);
    Task<AdminResult<AdminCourseDto>> UnpublishCourseAsync(int courseId);
    Task<AdminStatsDto> GetStatsAsync();
}