using Lms.Api.Data;
using Lms.Api.Dtos.Learning;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class DashboardService(AppDbContext db, IEnrollmentService enrollments) : IDashboardService
{
    private const int MaxEnrollments = 6;
    private const int MaxRecentQuizResults = 5;
    private const int MaxUpcomingAssignments = 5;

    private sealed record NextLesson(int Id, string Title);

    public async Task<DashboardDto> GetAsync(int userId)
    {
        // Reuse the enrollment list from step 3.2 (two queries, already includes live progress numbers).
        var all = await enrollments.ListAsync(userId);

        // Latest "lesson completed" time per course for this student, in one grouped query.
        var lastProgress = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.Lesson.Module.CourseId)
            .Select(g => new { CourseId = g.Key, Last = g.Max(p => p.CompletedAt) })
            .ToDictionaryAsync(x => x.CourseId, x => x.Last);

        var completedLessonsCount = await db.LessonProgresses.AsNoTracking().CountAsync(p => p.UserId == userId);

        // Activity of a course = the later of "enrolled" and "last lesson completed".
        DateTime Activity(EnrollmentDto e) =>
            lastProgress.TryGetValue(e.CourseId, out var last) && last > e.EnrolledAt ? last : e.EnrolledAt;

        var byActivity = all.OrderByDescending(Activity).ThenByDescending(e => e.Id).ToList();

        // "Completed" is computed live from the counts (same numbers the student sees),
        // not from the stored CompletedAt, which is only refreshed on the next complete/uncomplete.
        var completedCount = all.Count(e => ProgressCalculator.IsComplete(e.CompletedLessons, e.TotalLessons));

        ContinueLearningDto? continueLearning = null;
        foreach (var e in byActivity)
        {
            if (!e.IsAvailable) continue;
            if (ProgressCalculator.IsComplete(e.CompletedLessons, e.TotalLessons)) continue;

            var next = await NextLessonAsync(userId, e.CourseId);
            if (next is null) continue;

            continueLearning = new ContinueLearningDto(e.CourseId, e.Title, e.Slug, next.Id, next.Title, e.ProgressPercent);
            break;
        }

        // Phase 6: the student's most recent SUBMITTED quiz attempts, newest first.
        var recentRows = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.SubmittedAt != null)
            .OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.Id)
            .Take(MaxRecentQuizResults)
            .Select(a => new
            {
                a.Id,
                a.QuizId,
                QuizTitle = a.Quiz.Title,
                a.Quiz.CourseId,
                CourseTitle = a.Quiz.Course.Title,
                a.ScorePercent,
                a.Passed,
                a.SubmittedAt
            })
            .ToListAsync();

        var recentQuizResults = recentRows
            .Select(a => new RecentQuizResultDto(
                a.Id, a.QuizId, a.QuizTitle, a.CourseId, a.CourseTitle,
                a.ScorePercent ?? 0, a.Passed ?? false, a.SubmittedAt!.Value))
            .ToList();

        // Phase 7: published assignments of the student's available courses that were NOT submitted yet.
        // Overdue ones come first (late submissions are still accepted), then by the nearest due date;
        // assignments without a due date come last. Sorted in memory: the candidate list is small.
        var availableCourseIds = all.Where(e => e.IsAvailable).Select(e => e.CourseId).ToList();
        var submittedIds = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == userId)
            .Select(s => s.AssignmentId)
            .ToListAsync();
        var candidates = await db.Assignments.AsNoTracking()
            .Where(a => a.IsPublished && availableCourseIds.Contains(a.CourseId) && !submittedIds.Contains(a.Id))
            .Select(a => new { a.Id, a.CourseId, CourseTitle = a.Course.Title, a.Title, a.DueDate })
            .ToListAsync();
        var upcomingAssignments = candidates
            .OrderBy(a => a.DueDate is null).ThenBy(a => a.DueDate).ThenBy(a => a.Id)
            .Take(MaxUpcomingAssignments)
            .Select(a => new UpcomingAssignmentDto(a.Id, a.CourseId, a.CourseTitle, a.Title, a.DueDate))
            .ToList();

        return new DashboardDto(
            EnrolledCount: all.Count,
            CompletedCount: completedCount,
            InProgressCount: all.Count - completedCount,
            CompletedLessonsCount: completedLessonsCount,
            ContinueLearning: continueLearning,
            Enrollments: byActivity.Take(MaxEnrollments).ToList(),
            UpcomingAssignments: upcomingAssignments,
            RecentQuizResults: recentQuizResults);
    }

    // The first lesson the student has not completed, in course order
    // (modules by OrderIndex, then lessons by OrderIndex).
    private async Task<NextLesson?> NextLessonAsync(int userId, int courseId)
    {
        var done = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && p.Lesson.Module.CourseId == courseId)
            .Select(p => p.LessonId)
            .ToListAsync();

        var lesson = await db.Lessons.AsNoTracking()
            .Where(l => l.Module.CourseId == courseId && !done.Contains(l.Id))
            .OrderBy(l => l.Module.OrderIndex).ThenBy(l => l.Module.Id)
            .ThenBy(l => l.OrderIndex).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Title })
            .FirstOrDefaultAsync();

        return lesson is null ? null : new NextLesson(lesson.Id, lesson.Title);
    }
}