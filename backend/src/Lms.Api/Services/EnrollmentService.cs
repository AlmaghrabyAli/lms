using Lms.Api.Data;
using Lms.Api.Dtos.Learning;
using Lms.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class EnrollmentService(AppDbContext db) : IEnrollmentService
{
    // Raw numbers from the database; the percent is added in memory by ProgressCalculator.
    private sealed record EnrollmentRow(
        int Id, int CourseId, string Title, string Slug, string? ThumbnailUrl, CourseLevel Level,
        string Category, string InstructorName, DateTime EnrolledAt, DateTime? CompletedAt,
        bool IsPublished, int TotalLessons);

    public async Task<EnrollResult> EnrollAsync(int userId, int courseId)
    {
        // Only published courses can be joined; a draft or unknown id looks the same: 404.
        var published = await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.IsPublished);
        if (!published) return new EnrollResult(null, false, EnrollError.CourseNotFound);

        var exists = await db.Enrollments.AsNoTracking().AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
        var created = false;

        if (!exists)
        {
            // UserId always comes from the token, never from the request.
            db.Enrollments.Add(new Enrollment { UserId = userId, CourseId = courseId, EnrolledAt = DateTime.UtcNow });
            try
            {
                await db.SaveChangesAsync();
                created = true;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Race (two quick clicks): the unique index let the other request win. That is fine: already enrolled.
                db.ChangeTracker.Clear();
            }
        }

        var enrollment = (await QueryAsync(userId, courseId)).FirstOrDefault();
        return new EnrollResult(enrollment, created, EnrollError.None);
    }

    public Task<List<EnrollmentDto>> ListAsync(int userId) => QueryAsync(userId, null);

    // Two queries total, however many enrollments there are (no loop with one query per course).
    private async Task<List<EnrollmentDto>> QueryAsync(int userId, int? courseId)
    {
        var query = db.Enrollments.AsNoTracking().Where(e => e.UserId == userId);
        if (courseId.HasValue) query = query.Where(e => e.CourseId == courseId.Value);

        var rows = await query
            .OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.Id)
            .Select(e => new EnrollmentRow(
                e.Id, e.CourseId, e.Course.Title, e.Course.Slug, e.Course.ThumbnailUrl, e.Course.Level,
                e.Course.Category, e.Course.Instructor.FullName, e.EnrolledAt, e.CompletedAt,
                e.Course.IsPublished,
                e.Course.Modules.SelectMany(m => m.Lessons).Count()))
            .ToListAsync();

        // Completed lessons per course for this student, in one grouped query.
        var progress = db.LessonProgresses.AsNoTracking().Where(p => p.UserId == userId);
        if (courseId.HasValue) progress = progress.Where(p => p.Lesson.Module.CourseId == courseId.Value);
        var completed = await progress
            .GroupBy(p => p.Lesson.Module.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count);

        return rows.Select(r =>
        {
            var done = completed.GetValueOrDefault(r.CourseId);
            return new EnrollmentDto(
                r.Id, r.CourseId, r.Title, r.Slug, r.ThumbnailUrl, r.Level, r.Category, r.InstructorName,
                r.EnrolledAt, r.CompletedAt, done, r.TotalLessons,
                ProgressCalculator.Percent(done, r.TotalLessons),
                r.IsPublished); // IsAvailable: a draft course keeps the enrollment but cannot be opened
        }).ToList();
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}