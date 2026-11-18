using Lms.Api.Data;
using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class InstructorStudentsService(AppDbContext db) : IInstructorStudentsService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private sealed record StudentRow(int UserId, string FullName, string Email, DateTime EnrolledAt);

    public async Task<PagedResult<CourseStudentDto>?> ListAsync(int courseId, int instructorId, int page, int pageSize)
    {
        // Ownership first: someone else's course looks exactly like a missing course.
        var owns = await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        if (!owns) return null;

        page = Math.Max(page, 1);
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var totalLessons = await db.Lessons.AsNoTracking().CountAsync(l => l.Module.CourseId == courseId);

        var enrollments = db.Enrollments.AsNoTracking().Where(e => e.CourseId == courseId);
        var total = await enrollments.CountAsync();

        var rows = await enrollments
            .OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new StudentRow(e.UserId, e.User.FullName, e.User.Email, e.EnrolledAt))
            .ToListAsync();

        // Completed lessons and last activity for the students on THIS page only, in one grouped query (no loop).
        var userIds = rows.Select(r => r.UserId).ToList();
        var progress = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.Lesson.Module.CourseId == courseId && userIds.Contains(p.UserId))
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Last = g.Max(p => p.CompletedAt) })
            .ToDictionaryAsync(x => x.UserId);

        var items = rows.Select(r =>
        {
            var p = progress.GetValueOrDefault(r.UserId);
            var done = p?.Count ?? 0;
            DateTime? last = p?.Last;
            return new CourseStudentDto(
                r.UserId, r.FullName, r.Email, r.EnrolledAt, last, done, totalLessons,
                ProgressCalculator.Percent(done, totalLessons),
                ProgressCalculator.IsComplete(done, totalLessons));
        }).ToList();

        return new PagedResult<CourseStudentDto>(items, page, pageSize, total);
    }
}