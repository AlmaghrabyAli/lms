using System.Linq.Expressions;
using Lms.Api.Data;
using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Content;
using Lms.Api.Dtos.Courses;
using Lms.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class InstructorCourseService(AppDbContext db) : IInstructorCourseService
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 50;

    // One projection reused by list / create / update / publish, so the SQL only selects what the DTO needs.
    private static readonly Expression<Func<Course, InstructorCourseDto>> ToSummary = c => new InstructorCourseDto(
        c.Id, c.Title, c.Slug, c.Description, c.ThumbnailUrl, c.Level, c.Category, c.IsPublished,
        c.CreatedAt, c.UpdatedAt,
        c.Modules.Count,
        c.Modules.SelectMany(m => m.Lessons).Count());

    public async Task<PagedResult<InstructorCourseDto>> ListAsync(int instructorId, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Courses.AsNoTracking().Where(c => c.InstructorId == instructorId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToSummary)
            .ToListAsync();

        return new PagedResult<InstructorCourseDto>(items, page, pageSize, total);
    }

    public Task<InstructorCourseDetailDto?> GetAsync(int courseId, int instructorId) =>
        db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.InstructorId == instructorId) // ownership is part of the query
            .Select(c => new InstructorCourseDetailDto(
                c.Id, c.Title, c.Slug, c.Description, c.ThumbnailUrl, c.Level, c.Category, c.IsPublished,
                c.CreatedAt, c.UpdatedAt,
                c.Modules.OrderBy(m => m.OrderIndex).Select(m => new ModuleDto(
                    m.Id, m.Title, m.OrderIndex,
                    m.Lessons.OrderBy(l => l.OrderIndex).Select(l => new LessonDto(
                        l.Id, l.Title, l.Type, l.VideoUrl, l.Content, l.ExternalUrl, l.DurationMinutes, l.OrderIndex
                    )).ToList()
                )).ToList()))
            .FirstOrDefaultAsync();

    public async Task<InstructorCourseDto> CreateAsync(int instructorId, CreateCourseRequest request)
    {
        // The slug is checked first (friendly), but two requests can still pick the same slug at the same time.
        // The unique index is the final guard: on a violation we pick a new slug and try again.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = DateTime.UtcNow;
            var course = new Course
            {
                Title = request.Title.Trim(),
                Slug = await GenerateUniqueSlugAsync(request.Title),
                Description = request.Description.Trim(),
                ThumbnailUrl = NormalizeUrl(request.ThumbnailUrl),
                Category = request.Category.Trim(),
                Level = request.Level,
                InstructorId = instructorId, // always from the token, never from the request body
                IsPublished = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Courses.Add(course);
            try
            {
                await db.SaveChangesAsync();
                return (await SummaryAsync(course.Id, instructorId))!;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.Entry(course).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Could not generate a unique course slug after 3 attempts.");
    }

    public async Task<InstructorCourseDto?> UpdateAsync(int courseId, int instructorId, UpdateCourseRequest request)
    {
        var course = await GetOwnedCourseAsync(courseId, instructorId);
        if (course is null) return null;

        course.Title = request.Title.Trim();
        course.Description = request.Description.Trim();
        course.Category = request.Category.Trim();
        course.Level = request.Level;
        course.ThumbnailUrl = NormalizeUrl(request.ThumbnailUrl);
        course.UpdatedAt = DateTime.UtcNow;
        // Slug and IsPublished are NOT touched here: stable URLs, and publishing has its own endpoint.

        await db.SaveChangesAsync();
        return await SummaryAsync(courseId, instructorId);
    }

    public async Task<CourseActionResult> PublishAsync(int courseId, int instructorId)
    {
        var course = await GetOwnedCourseAsync(courseId, instructorId);
        if (course is null) return new CourseActionResult(null, CourseActionError.NotFound);

        if (!course.IsPublished)
        {
            // A lesson always sits inside a module, so "has a lesson" also means "has a module".
            var hasLesson = await db.Lessons.AnyAsync(l => l.Module.CourseId == courseId);
            if (!hasLesson) return new CourseActionResult(null, CourseActionError.NotPublishable);

            course.IsPublished = true;
            course.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return new CourseActionResult(await SummaryAsync(courseId, instructorId), CourseActionError.None);
    }

    public async Task<CourseActionResult> UnpublishAsync(int courseId, int instructorId)
    {
        var course = await GetOwnedCourseAsync(courseId, instructorId);
        if (course is null) return new CourseActionResult(null, CourseActionError.NotFound);

        if (course.IsPublished)
        {
            course.IsPublished = false;
            course.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return new CourseActionResult(await SummaryAsync(courseId, instructorId), CourseActionError.None);
    }

    public async Task<CourseActionError> DeleteAsync(int courseId, int instructorId)
    {
        var course = await GetOwnedCourseAsync(courseId, instructorId);
        if (course is null) return CourseActionError.NotFound;

        // Phase 2 rule: only drafts can be deleted. (Phase 3 adds: and only if nobody is enrolled.)
        if (course.IsPublished) return CourseActionError.PublishedCannotBeDeleted;

        db.Courses.Remove(course); // the database cascade removes its modules and lessons
        await db.SaveChangesAsync();
        return CourseActionError.None;
    }

    // Resource-based authorization: the course is loaded only if it belongs to this instructor.
    // Tracked (no AsNoTracking) because callers modify it.
    private Task<Course?> GetOwnedCourseAsync(int courseId, int instructorId) =>
        db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);

    private Task<InstructorCourseDto?> SummaryAsync(int courseId, int instructorId) =>
        db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.InstructorId == instructorId)
            .Select(ToSummary)
            .FirstOrDefaultAsync();

    private async Task<string> GenerateUniqueSlugAsync(string title)
    {
        var baseSlug = SlugHelper.Slugify(title);
        var candidate = baseSlug;
        var n = 2;
        while (await db.Courses.AnyAsync(c => c.Slug == candidate))
        {
            candidate = $"{baseSlug}-{n}";
            n++;
        }
        return candidate;
    }

    private static string? NormalizeUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}