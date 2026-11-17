using Lms.Api.Dtos.Learning;

namespace Lms.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(int userId);
}