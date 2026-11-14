using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;
using Lms.Api.Entities;

namespace Lms.Api.Services;

public interface ICatalogService
{
    Task<PagedResult<CourseSummaryDto>> ListAsync(string? search, string? category, CourseLevel? level, int page, int pageSize);
    Task<List<string>> CategoriesAsync();
    Task<CourseDetailDto?> GetBySlugAsync(string slug);
}