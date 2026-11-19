using Lms.Api.Data;
using Lms.Api.Dtos.Assignments;
using Lms.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class AssignmentTakingService(AppDbContext db) : IAssignmentTakingService
{
    private sealed record Access(AssignmentError Error, bool IsStudent);
    private sealed record AssignmentInfo(int Id, int CourseId, string Title, string Instructions, int? ModuleId, DateTime? DueDate, int MaxScore);
    private sealed record SubmissionRow(int Id, int AssignmentId, string? TextContent, string? LinkUrl, DateTime SubmittedAt, int? Score, string? Feedback, DateTime? GradedAt);

    // ======================= Read =======================

    public async Task<AssignmentResult<List<AssignmentListItemDto>>> ListForCourseAsync(int courseId, int userId, string role)
    {
        var access = await ResolveAccessAsync(courseId, userId, role);
        if (access.Error != AssignmentError.None) return new AssignmentResult<List<AssignmentListItemDto>>(null, access.Error);

        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.CourseId == courseId && a.IsPublished)
            .OrderBy(a => a.Id)
            .Select(a => new AssignmentInfo(a.Id, a.CourseId, a.Title, a.Instructions, a.ModuleId, a.DueDate, a.MaxScore))
            .ToListAsync();

        var submissions = new List<SubmissionRow>();
        if (access.IsStudent)
        {
            var ids = assignments.Select(a => a.Id).ToList();
            submissions = await db.Submissions.AsNoTracking()
                .Where(s => s.StudentId == userId && ids.Contains(s.AssignmentId))
                .Select(s => new SubmissionRow(s.Id, s.AssignmentId, s.TextContent, s.LinkUrl, s.SubmittedAt, s.Score, s.Feedback, s.GradedAt))
                .ToListAsync();
        }

        var items = assignments.Select(a =>
        {
            var mine = submissions.FirstOrDefault(s => s.AssignmentId == a.Id);
            return new AssignmentListItemDto(
                a.Id, a.CourseId, a.Title, a.ModuleId, a.DueDate, a.MaxScore,
                StatusOf(mine), mine?.Score, mine?.SubmittedAt, IsLate(mine?.SubmittedAt, a.DueDate));
        }).ToList();

        return new AssignmentResult<List<AssignmentListItemDto>>(items, AssignmentError.None);
    }

    public async Task<AssignmentResult<StudentAssignmentDto>> GetAsync(int assignmentId, int userId, string role)
    {
        var gate = await GateAsync(assignmentId, userId, role);
        if (gate.Error != AssignmentError.None) return new AssignmentResult<StudentAssignmentDto>(null, gate.Error);
        var a = gate.Assignment!;

        SubmissionRow? mine = null;
        if (gate.IsStudent)
            mine = await LoadSubmissionAsync(assignmentId, userId);

        return new AssignmentResult<StudentAssignmentDto>(
            new StudentAssignmentDto(
                a.Id, a.CourseId, a.Title, a.Instructions, a.ModuleId, a.DueDate, a.MaxScore,
                StatusOf(mine), mine is null ? null : ToDto(mine, a)),
            AssignmentError.None);
    }

    public async Task<AssignmentResult<SubmissionDto>> MySubmissionAsync(int assignmentId, int userId)
    {
        var gate = await GateAsync(assignmentId, userId, Roles.Student);
        if (gate.Error != AssignmentError.None) return new AssignmentResult<SubmissionDto>(null, gate.Error);

        var mine = await LoadSubmissionAsync(assignmentId, userId);
        if (mine is null) return new AssignmentResult<SubmissionDto>(null, AssignmentError.NoSubmission);

        return new AssignmentResult<SubmissionDto>(ToDto(mine, gate.Assignment!), AssignmentError.None);
    }

    // ======================= Submit =======================

    public async Task<SubmitResult> SubmitAsync(int assignmentId, int userId, SubmitAssignmentRequest request)
    {
        var gate = await GateAsync(assignmentId, userId, Roles.Student);
        if (gate.Error != AssignmentError.None) return new SubmitResult(null, gate.Error, false);
        var info = gate.Assignment!;

        var text = Clean(request.TextContent);
        var link = Clean(request.LinkUrl);

        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == userId);
        if (submission is not null && submission.GradedAt is not null)
            return new SubmitResult(null, AssignmentError.AlreadyGraded, false);

        var created = submission is null;
        if (submission is null)
        {
            submission = new Submission { AssignmentId = assignmentId, StudentId = userId };   // ids from the URL and the JWT
            db.Submissions.Add(submission);
        }
        submission.TextContent = text;
        submission.LinkUrl = link;
        submission.SubmittedAt = DateTime.UtcNow;      // a late resubmission is late again

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two quick submits: the unique (AssignmentId, StudentId) index let only one create the row. Update that one.
            db.Entry(submission).State = EntityState.Detached;
            submission = await db.Submissions.FirstAsync(s => s.AssignmentId == assignmentId && s.StudentId == userId);
            if (submission.GradedAt is not null) return new SubmitResult(null, AssignmentError.AlreadyGraded, false);
            submission.TextContent = text;
            submission.LinkUrl = link;
            submission.SubmittedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            created = false;
        }

        var row = new SubmissionRow(submission.Id, assignmentId, submission.TextContent, submission.LinkUrl,
            submission.SubmittedAt, submission.Score, submission.Feedback, submission.GradedAt);
        return new SubmitResult(ToDto(row, info), AssignmentError.None, created);
    }

    // ======================= Helpers =======================

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static bool IsLate(DateTime? submittedAt, DateTime? dueDate) =>
        submittedAt is not null && dueDate is not null && submittedAt > dueDate;

    private static SubmissionStatus StatusOf(SubmissionRow? s) =>
        s is null ? SubmissionStatus.NotSubmitted
        : s.GradedAt is not null ? SubmissionStatus.Graded
        : SubmissionStatus.Submitted;

    private static SubmissionDto ToDto(SubmissionRow s, AssignmentInfo a) =>
        new(s.Id, s.TextContent, s.LinkUrl, s.SubmittedAt, IsLate(s.SubmittedAt, a.DueDate), s.Score, a.MaxScore, s.Feedback, s.GradedAt);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private Task<SubmissionRow?> LoadSubmissionAsync(int assignmentId, int userId) =>
        db.Submissions.AsNoTracking()
            .Where(s => s.AssignmentId == assignmentId && s.StudentId == userId)
            .Select(s => new SubmissionRow(s.Id, s.AssignmentId, s.TextContent, s.LinkUrl, s.SubmittedAt, s.Score, s.Feedback, s.GradedAt))
            .FirstOrDefaultAsync();

    // Same resource-based rule as the learn view and the quizzes: the course must be published (else 404),
    // then the role decides (student must be enrolled, instructor must own the course, admin is allowed).
    private async Task<Access> ResolveAccessAsync(int courseId, int userId, string role)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.IsPublished)
            .Select(c => new { c.InstructorId })
            .FirstOrDefaultAsync();
        if (course is null) return new Access(AssignmentError.NotFound, false);

        switch (role)
        {
            case Roles.Student:
                var enrolled = await db.Enrollments.AsNoTracking().AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
                return enrolled ? new Access(AssignmentError.None, true) : new Access(AssignmentError.NotEnrolled, false);
            case Roles.Instructor:
                return course.InstructorId == userId ? new Access(AssignmentError.None, false) : new Access(AssignmentError.NoAccess, false);
            case Roles.Admin:
                return new Access(AssignmentError.None, false);
            default:
                return new Access(AssignmentError.NoAccess, false);
        }
    }

    // assignment -> must exist AND be published (else 404), then the course access rule above.
    private async Task<(AssignmentError Error, AssignmentInfo? Assignment, bool IsStudent)> GateAsync(int assignmentId, int userId, string role)
    {
        var assignment = await db.Assignments.AsNoTracking()
            .Where(a => a.Id == assignmentId && a.IsPublished)
            .Select(a => new AssignmentInfo(a.Id, a.CourseId, a.Title, a.Instructions, a.ModuleId, a.DueDate, a.MaxScore))
            .FirstOrDefaultAsync();
        if (assignment is null) return (AssignmentError.NotFound, null, false);

        var access = await ResolveAccessAsync(assignment.CourseId, userId, role);
        return (access.Error, access.Error == AssignmentError.None ? assignment : null, access.IsStudent);
    }
}