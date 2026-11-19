using Lms.Api.Data;
using Lms.Api.Dtos.Assignments;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class InstructorAssignmentService(AppDbContext db) : IInstructorAssignmentService
{
    public async Task<List<AssignmentSummaryDto>?> ListAsync(int courseId, int instructorId)
    {
        var owns = await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        if (!owns) return null;

        var rows = await db.Assignments.AsNoTracking()
            .Where(a => a.CourseId == courseId)
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.ModuleId,
                ModuleTitle = a.Module == null ? null : a.Module.Title,
                a.DueDate,
                a.MaxScore,
                a.IsPublished
            })
            .ToListAsync();

        // Submission counts for these assignments, grouped in memory (a course has few assignments).
        var ids = rows.Select(r => r.Id).ToList();
        var subs = await db.Submissions.AsNoTracking()
            .Where(s => ids.Contains(s.AssignmentId))
            .Select(s => new { s.AssignmentId, Graded = s.GradedAt != null })
            .ToListAsync();

        return rows.Select(a => new AssignmentSummaryDto(
            a.Id, a.Title, a.ModuleId, a.ModuleTitle, a.DueDate, a.MaxScore, a.IsPublished,
            subs.Count(s => s.AssignmentId == a.Id),
            subs.Count(s => s.AssignmentId == a.Id && s.Graded))).ToList();
    }

    public async Task<AssignmentResult<AssignmentDetailDto>> CreateAsync(int courseId, int instructorId, CreateAssignmentRequest request)
    {
        // Ownership: someone else's course looks exactly like a missing course.
        var owns = await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        if (!owns) return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.NotFound);

        if (request.ModuleId is not null &&
            !await db.Modules.AnyAsync(m => m.Id == request.ModuleId && m.CourseId == courseId))
            return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.InvalidModule);

        var assignment = new Assignment
        {
            CourseId = courseId,                         // from the URL, never from the body
            ModuleId = request.ModuleId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions.Trim(),
            DueDate = request.DueDate?.ToUniversalTime(),
            MaxScore = request.MaxScore
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return new AssignmentResult<AssignmentDetailDto>(await BuildDetailAsync(assignment.Id), AssignmentError.None);
    }

    public async Task<AssignmentResult<AssignmentDetailDto>> GetAsync(int assignmentId, int instructorId)
    {
        var owns = await db.Assignments.AsNoTracking().AnyAsync(a => a.Id == assignmentId && a.Course.InstructorId == instructorId);
        if (!owns) return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.NotFound);
        return new AssignmentResult<AssignmentDetailDto>(await BuildDetailAsync(assignmentId), AssignmentError.None);
    }

    public async Task<AssignmentResult<AssignmentDetailDto>> UpdateAsync(int assignmentId, int instructorId, UpdateAssignmentRequest request)
    {
        var assignment = await OwnedAsync(assignmentId, instructorId);
        if (assignment is null) return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.NotFound);

        if (request.ModuleId is not null &&
            !await db.Modules.AnyAsync(m => m.Id == request.ModuleId && m.CourseId == assignment.CourseId))
            return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.InvalidModule);

        // A graded score must always fit inside MaxScore.
        var highestGraded = await db.Submissions
            .Where(s => s.AssignmentId == assignmentId && s.Score != null)
            .MaxAsync(s => (int?)s.Score);
        if (highestGraded is not null && request.MaxScore < highestGraded)
            return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.ScoreConflict,
                $"A graded submission already has {highestGraded} points, so MaxScore cannot be lower than that.");

        assignment.Title = request.Title.Trim();
        assignment.Instructions = request.Instructions.Trim();
        assignment.DueDate = request.DueDate?.ToUniversalTime();
        assignment.MaxScore = request.MaxScore;
        assignment.ModuleId = request.ModuleId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new AssignmentResult<AssignmentDetailDto>(await BuildDetailAsync(assignmentId), AssignmentError.None);
    }

    public async Task<AssignmentError> DeleteAsync(int assignmentId, int instructorId)
    {
        var assignment = await OwnedAsync(assignmentId, instructorId);
        if (assignment is null) return AssignmentError.NotFound;
        if (await db.Submissions.AnyAsync(s => s.AssignmentId == assignmentId)) return AssignmentError.HasSubmissions;

        db.Assignments.Remove(assignment);
        await db.SaveChangesAsync();
        return AssignmentError.None;
    }

    public async Task<AssignmentResult<AssignmentDetailDto>> SetPublishedAsync(int assignmentId, int instructorId, bool publish)
    {
        var assignment = await OwnedAsync(assignmentId, instructorId);
        if (assignment is null) return new AssignmentResult<AssignmentDetailDto>(null, AssignmentError.NotFound);

        assignment.IsPublished = publish;
        assignment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new AssignmentResult<AssignmentDetailDto>(await BuildDetailAsync(assignmentId), AssignmentError.None);
    }

    public async Task<AssignmentResult<List<SubmissionRowDto>>> ListSubmissionsAsync(int assignmentId, int instructorId)
    {
        var assignment = await db.Assignments.AsNoTracking()
            .Where(a => a.Id == assignmentId && a.Course.InstructorId == instructorId)
            .Select(a => new { a.DueDate })
            .FirstOrDefaultAsync();
        if (assignment is null) return new AssignmentResult<List<SubmissionRowDto>>(null, AssignmentError.NotFound);

        var rows = await db.Submissions.AsNoTracking()
            .Where(s => s.AssignmentId == assignmentId)
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.StudentId,
                s.Student.FullName,
                s.Student.Email,
                s.TextContent,
                s.LinkUrl,
                s.SubmittedAt,
                s.Score,
                s.Feedback,
                s.GradedAt
            })
            .ToListAsync();

        var list = rows.Select(r => new SubmissionRowDto(
            r.Id, r.StudentId, r.FullName, r.Email, r.TextContent, r.LinkUrl, r.SubmittedAt,
            assignment.DueDate is not null && r.SubmittedAt > assignment.DueDate,
            r.Score, r.Feedback, r.GradedAt)).ToList();

        return new AssignmentResult<List<SubmissionRowDto>>(list, AssignmentError.None);
    }

    public async Task<AssignmentResult<SubmissionRowDto>> GradeAsync(int submissionId, int instructorId, GradeSubmissionRequest request)
    {
        // Ownership goes submission -> assignment -> course -> instructor.
        var submission = await db.Submissions.Include(s => s.Assignment).Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.Assignment.Course.InstructorId == instructorId);
        if (submission is null) return new AssignmentResult<SubmissionRowDto>(null, AssignmentError.NotFound);

        if (request.Score > submission.Assignment.MaxScore)
            return new AssignmentResult<SubmissionRowDto>(null, AssignmentError.InvalidScore,
                $"The score must be between 0 and {submission.Assignment.MaxScore}.");

        submission.Score = request.Score;
        submission.Feedback = string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim();
        submission.GradedAt = DateTime.UtcNow;     // grading again is allowed and simply refreshes this
        await db.SaveChangesAsync();

        var due = submission.Assignment.DueDate;
        return new AssignmentResult<SubmissionRowDto>(
            new SubmissionRowDto(
                submission.Id, submission.StudentId, submission.Student.FullName, submission.Student.Email,
                submission.TextContent, submission.LinkUrl, submission.SubmittedAt,
                due is not null && submission.SubmittedAt > due,
                submission.Score, submission.Feedback, submission.GradedAt),
            AssignmentError.None);
    }

    // ======================= Helpers =======================

    private Task<Assignment?> OwnedAsync(int assignmentId, int instructorId) =>
        db.Assignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.Course.InstructorId == instructorId);

    private async Task<AssignmentDetailDto> BuildDetailAsync(int assignmentId)
    {
        var a = await db.Assignments.AsNoTracking()
            .Where(x => x.Id == assignmentId)
            .Select(x => new { x.Id, x.CourseId, x.ModuleId, x.Title, x.Instructions, x.DueDate, x.MaxScore, x.IsPublished })
            .FirstAsync();
        var total = await db.Submissions.AsNoTracking().CountAsync(s => s.AssignmentId == assignmentId);
        var graded = await db.Submissions.AsNoTracking().CountAsync(s => s.AssignmentId == assignmentId && s.GradedAt != null);

        return new AssignmentDetailDto(a.Id, a.CourseId, a.ModuleId, a.Title, a.Instructions, a.DueDate, a.MaxScore, a.IsPublished, total, graded);
    }
}