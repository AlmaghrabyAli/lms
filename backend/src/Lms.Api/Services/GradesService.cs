using Lms.Api.Data;
using Lms.Api.Dtos.Learning;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

// Everything the "My grades" page needs: the student's own submissions and the best result per quiz.
public class GradesService(AppDbContext db) : IGradesService
{
    public async Task<GradesDto> GetAsync(int userId)
    {
        var submissions = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == userId)
            .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.AssignmentId,
                s.Assignment.CourseId,
                CourseTitle = s.Assignment.Course.Title,
                AssignmentTitle = s.Assignment.Title,
                s.Assignment.MaxScore,
                s.Assignment.DueDate,
                s.Score,
                s.Feedback,
                s.SubmittedAt,
                s.GradedAt
            })
            .ToListAsync();

        var assignments = submissions.Select(s => new AssignmentGradeDto(
            s.Id, s.AssignmentId, s.CourseId, s.CourseTitle, s.AssignmentTitle, s.MaxScore, s.Score, s.Feedback,
            s.SubmittedAt, s.DueDate is not null && s.SubmittedAt > s.DueDate, s.GradedAt)).ToList();

        // Submitted quiz attempts only, grouped per quiz in memory (a student has few attempts).
        var attempts = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.SubmittedAt != null)
            .Select(a => new
            {
                a.QuizId,
                QuizTitle = a.Quiz.Title,
                a.Quiz.CourseId,
                CourseTitle = a.Quiz.Course.Title,
                a.ScorePercent,
                a.Passed,
                a.SubmittedAt
            })
            .ToListAsync();

        var quizzes = attempts
            .GroupBy(a => a.QuizId)
            .Select(g => new QuizGradeDto(
                g.Key, g.First().CourseId, g.First().CourseTitle, g.First().QuizTitle,
                g.Count(), g.Max(x => x.ScorePercent ?? 0), g.Any(x => x.Passed == true), g.Max(x => x.SubmittedAt)))
            .OrderByDescending(q => q.LastSubmittedAt)
            .ToList();

        return new GradesDto(assignments, quizzes);
    }
}