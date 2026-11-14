using System.Linq.Expressions;
using Lms.Api.Data;
using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class CatalogService(AppDbContext db) : ICatalogService
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 50;
    private const int MaxSearchLength = 100;

    private static readonly Expression<Func<Course, CourseSummaryDto>> ToSummary = c => new CourseSummaryDto(
        c.Id, c.Title, c.Slug, c.Description, c.ThumbnailUrl, c.Level, c.Category,
        c.Instructor.FullName, // the name only, never the email
        c.Modules.Count,
        c.Modules.SelectMany(m => m.Lessons).Count(),
        c.Modules.SelectMany(m => m.Lessons).Sum(l => l.DurationMinutes ?? 0));

    public async Task<PagedResult<CourseSummaryDto>> ListAsync(string? search, string? category, CourseLevel? level, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        // The catalog starts from "published only"; every filter below narrows it further.
        var query = db.Courses.AsNoTracking().Where(c => c.IsPublished);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > MaxSearchLength) term = term[..MaxSearchLength];
            var lower = term.ToLower(); // LOWER() on both sides: case-insensitive whatever the DB collation is
            query = query.Where(c => c.Title.ToLower().Contains(lower) || c.Description.ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLower();
            query = query.Where(c => c.Category.ToLower() == cat);
        }

        if (level is not null)
        {
            var lv = level.Value;
            query = query.Where(c => c.Level == lv);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToSummary)
            .ToListAsync();

        return new PagedResult<CourseSummaryDto>(items, page, pageSize, total);
    }

    public Task<List<string>> CategoriesAsync() =>
        db.Courses.AsNoTracking()
            .Where(c => c.IsPublished)
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    public Task<CourseDetailDto?> GetBySlugAsync(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        // Drafts are invisible here for everybody, even for their owner.
        return db.Courses.AsNoTracking()
            .Where(c => c.Slug == normalized && c.IsPublished)
            .Select(c => new CourseDetailDto(
                c.Id, c.Title, c.Slug, c.Description, c.ThumbnailUrl, c.Level, c.Category,
                c.Instructor.FullName,
                c.CreatedAt, c.UpdatedAt,
                c.Modules.Count,
                c.Modules.SelectMany(m => m.Lessons).Count(),
                c.Modules.SelectMany(m => m.Lessons).Sum(l => l.DurationMinutes ?? 0),
                c.Modules.OrderBy(m => m.OrderIndex).ThenBy(m => m.Id).Select(m => new PublicModuleDto(
                    m.Id, m.Title, m.OrderIndex,
                    m.Lessons.OrderBy(l => l.OrderIndex).ThenBy(l => l.Id).Select(l => new LessonOutlineDto(
                        l.Id, l.Title, l.Type, l.DurationMinutes, l.OrderIndex
                    )).ToList()
                )).ToList()))
            .FirstOrDefaultAsync();
    }
}