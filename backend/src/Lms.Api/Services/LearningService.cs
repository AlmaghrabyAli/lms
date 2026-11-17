using Lms.Api.Data;
using Lms.Api.Dtos.Learning;
using Lms.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class LearningService(AppDbContext db) : ILearningService
{
    private sealed record Access(LearningError Error, bool IsStudent);

    // ======================= Learn view =======================

    public async Task<LearnResult> GetLearnAsync(int courseId, int userId, string role)
    {
        var access = await ResolveAccessAsync(courseId, userId, role);
        if (access.Error != LearningError.None) return new LearnResult(null, access.Error);

        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new { c.Title, c.Slug })
            .FirstAsync();

        var modules = await db.Modules.AsNoTracking()
            .Where(m => m.CourseId == courseId)
            .OrderBy(m => m.OrderIndex).ThenBy(m => m.Id)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.OrderIndex,
                Lessons = m.Lessons
                    .OrderBy(l => l.OrderIndex).ThenBy(l => l.Id)
                    .Select(l => new { l.Id, l.Title, l.Type, l.DurationMinutes, l.OrderIndex })
                    .ToList()
            })
            .ToListAsync();

        // Students get their real progress; instructor/admin get a read-only preview (nothing is completed).
        var done = new HashSet<int>();
        DateTime? enrollmentCompletedAt = null;
        if (access.IsStudent)
        {
            done = new HashSet<int>(await db.LessonProgresses.AsNoTracking()
                .Where(p => p.UserId == userId && p.Lesson.Module.CourseId == courseId)
                .Select(p => p.LessonId)
                .ToListAsync());
            enrollmentCompletedAt = await db.Enrollments.AsNoTracking()
                .Where(e => e.UserId == userId && e.CourseId == courseId)
                .Select(e => e.CompletedAt)
                .FirstOrDefaultAsync();
        }

        var moduleDtos = modules.Select(m => new LearnModuleDto(
            m.Id, m.Title, m.OrderIndex,
            m.Lessons.Select(l => new LearnLessonDto(l.Id, l.Title, l.Type, l.DurationMinutes, l.OrderIndex, done.Contains(l.Id))).ToList()
        )).ToList();

        var total = moduleDtos.Sum(m => m.Lessons.Count);
        var completed = moduleDtos.Sum(m => m.Lessons.Count(l => l.IsCompleted));

        return new LearnResult(
            new LearnCourseDto(courseId, course.Title, course.Slug,
                ProgressCalculator.Percent(completed, total), completed, total, enrollmentCompletedAt, moduleDtos),
            LearningError.None);
    }

    // ======================= Lesson view =======================

    public async Task<LessonViewResult> GetLessonAsync(int lessonId, int userId, string role)
    {
        var lesson = await db.Lessons.AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => new
            {
                l.Id, l.ModuleId, CourseId = l.Module.CourseId, l.Title, l.Type,
                l.VideoUrl, l.Content, l.ExternalUrl, l.DurationMinutes, l.OrderIndex
            })
            .FirstOrDefaultAsync();
        if (lesson is null) return new LessonViewResult(null, LearningError.NotFound);

        var access = await ResolveAccessAsync(lesson.CourseId, userId, role);
        if (access.Error != LearningError.None) return new LessonViewResult(null, access.Error);

        var isCompleted = access.IsStudent
            && await db.LessonProgresses.AsNoTracking().AnyAsync(p => p.UserId == userId && p.LessonId == lessonId);

        // Course order: modules by OrderIndex, then lessons by OrderIndex, across module boundaries.
        var orderedIds = await db.Lessons.AsNoTracking()
            .Where(l => l.Module.CourseId == lesson.CourseId)
            .OrderBy(l => l.Module.OrderIndex).ThenBy(l => l.Module.Id)
            .ThenBy(l => l.OrderIndex).ThenBy(l => l.Id)
            .Select(l => l.Id)
            .ToListAsync();

        var index = orderedIds.IndexOf(lessonId);
        int? previous = index > 0 ? orderedIds[index - 1] : null;
        int? next = index >= 0 && index < orderedIds.Count - 1 ? orderedIds[index + 1] : null;

        return new LessonViewResult(
            new LessonViewDto(lesson.Id, lesson.CourseId, lesson.ModuleId, lesson.Title, lesson.Type,
                lesson.VideoUrl, lesson.Content, lesson.ExternalUrl, lesson.DurationMinutes, lesson.OrderIndex,
                isCompleted, previous, next),
            LearningError.None);
    }

    // ======================= Complete / uncomplete =======================

    public async Task<ProgressResult> CompleteAsync(int lessonId, int userId)
    {
        var gate = await GateForStudentAsync(lessonId, userId);
        if (gate.Error != LearningError.None) return new ProgressResult(null, gate.Error);

        var exists = await db.LessonProgresses.AsNoTracking().AnyAsync(p => p.UserId == userId && p.LessonId == lessonId);
        if (!exists)
        {
            db.LessonProgresses.Add(new LessonProgress { UserId = userId, LessonId = lessonId, CompletedAt = DateTime.UtcNow });
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear(); // two quick clicks: the other request already stored it, which is fine
            }
        }

        return new ProgressResult(await BuildProgressAsync(userId, gate.CourseId, lessonId), LearningError.None);
    }

    public async Task<ProgressResult> UncompleteAsync(int lessonId, int userId)
    {
        var gate = await GateForStudentAsync(lessonId, userId);
        if (gate.Error != LearningError.None) return new ProgressResult(null, gate.Error);

        var row = await db.LessonProgresses.FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);
        if (row is not null)
        {
            db.LessonProgresses.Remove(row);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear(); // a parallel request already removed it: nothing left to do
            }
        }

        return new ProgressResult(await BuildProgressAsync(userId, gate.CourseId, lessonId), LearningError.None);
    }

    // ======================= Helpers =======================

    // Resource-based authorization in one place: the course must exist AND be published (else 404),
    // then the role decides: student must be enrolled, instructor must own the course, admin is allowed.
    private async Task<Access> ResolveAccessAsync(int courseId, int userId, string role)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.IsPublished)
            .Select(c => new { c.InstructorId })
            .FirstOrDefaultAsync();
        if (course is null) return new Access(LearningError.NotFound, false);

        switch (role)
        {
            case Roles.Student:
                var enrolled = await db.Enrollments.AsNoTracking().AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
                return enrolled ? new Access(LearningError.None, true) : new Access(LearningError.NotEnrolled, false);
            case Roles.Instructor:
                return course.InstructorId == userId ? new Access(LearningError.None, false) : new Access(LearningError.NoAccess, false);
            case Roles.Admin:
                return new Access(LearningError.None, false);
            default:
                return new Access(LearningError.NoAccess, false);
        }
    }

    // lesson -> course, then the student access rule (used by complete and uncomplete)
    private async Task<(LearningError Error, int CourseId)> GateForStudentAsync(int lessonId, int userId)
    {
        var courseId = await db.Lessons.AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => (int?)l.Module.CourseId)
            .FirstOrDefaultAsync();
        if (courseId is null) return (LearningError.NotFound, 0);

        var access = await ResolveAccessAsync(courseId.Value, userId, Roles.Student);
        return (access.Error, courseId.Value);
    }

    // Recomputes the numbers from the database and applies the D3 rule to Enrollment.CompletedAt:
    // 100% -> set (if empty), below 100% -> cleared. Runs after EVERY complete/uncomplete.
    private async Task<ProgressDto> BuildProgressAsync(int userId, int courseId, int lessonId)
    {
        var total = await db.Lessons.AsNoTracking().CountAsync(l => l.Module.CourseId == courseId);
        var completed = await db.LessonProgresses.AsNoTracking()
            .CountAsync(p => p.UserId == userId && p.Lesson.Module.CourseId == courseId);
        var lessonDone = await db.LessonProgresses.AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.LessonId == lessonId);

        var enrollment = await db.Enrollments.FirstAsync(e => e.UserId == userId && e.CourseId == courseId);
        var isComplete = ProgressCalculator.IsComplete(completed, total);
        if (isComplete && enrollment.CompletedAt is null)
        {
            enrollment.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        else if (!isComplete && enrollment.CompletedAt is not null)
        {
            enrollment.CompletedAt = null;
            await db.SaveChangesAsync();
        }

        return new ProgressDto(courseId, lessonId, lessonDone, completed, total,
            ProgressCalculator.Percent(completed, total),
            enrollment.CompletedAt is not null, enrollment.CompletedAt);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}