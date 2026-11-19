using Lms.Api.Dtos.Learning;

namespace Lms.Api.Services;

public interface IGradesService
{
    Task<GradesDto> GetAsync(int userId);
}