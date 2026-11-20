using Lms.Api.Data;
using Lms.Api.Dtos.Assignments;
using Lms.Api.Entities;
using Lms.Api.Services;
using Lms.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lms.Tests.Services;

public class AssignmentTakingServiceTests
{
    private sealed record Setup(User Instructor, User Student, Course Course, Assignment Assignment);

    private static async Task<Setup> ArrangeAsync(AppDbContext db, DateTime? due = null, bool published = true, bool enroll = true)
    {
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var course = await TestDb.AddCourseAsync(db, instructor);
        if (enroll) await TestDb.EnrollAsync(db, student, course);
        var assignment = await TestDb.AddAssignmentAsync(db, course, due, published);
        return new Setup(instructor, student, course, assignment);
    }

    private static SubmitAssignmentRequest Text(string text) => new() { TextContent = text };

    [Fact]
    public async Task The_first_submission_is_created_and_a_resubmission_overwrites_it()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new AssignmentTakingService(db);

        var first = await service.SubmitAsync(s.Assignment.Id, s.Student.Id, Text("first"));
        var second = await service.SubmitAsync(s.Assignment.Id, s.Student.Id, Text("second"));

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Submission!.Id, second.Submission!.Id);
        Assert.Equal("second", second.Submission.TextContent);
        Assert.Equal(1, await db.Submissions.CountAsync());
    }

    [Fact]
    public async Task A_graded_submission_cannot_be_changed_by_the_student()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new AssignmentTakingService(db);
        await service.SubmitAsync(s.Assignment.Id, s.Student.Id, Text("first"));

        var submission = await db.Submissions.SingleAsync();
        submission.Score = 80;
        submission.GradedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await service.SubmitAsync(s.Assignment.Id, s.Student.Id, Text("changed"));

        Assert.Equal(AssignmentError.AlreadyGraded, result.Error);
        Assert.Equal("first", (await db.Submissions.SingleAsync()).TextContent);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public async Task A_submission_is_late_only_after_the_due_date(int daysFromNow, bool expectedLate)
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, due: DateTime.UtcNow.AddDays(daysFromNow));

        var result = await new AssignmentTakingService(db).SubmitAsync(s.Assignment.Id, s.Student.Id, Text("work"));

        Assert.Equal(AssignmentError.None, result.Error);
        Assert.Equal(expectedLate, result.Submission!.IsLate);
    }

    [Fact]
    public async Task An_assignment_without_a_due_date_is_never_late()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, due: null);

        var result = await new AssignmentTakingService(db).SubmitAsync(s.Assignment.Id, s.Student.Id, Text("work"));

        Assert.False(result.Submission!.IsLate);
    }

    [Fact]
    public async Task A_student_who_is_not_enrolled_cannot_submit()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, enroll: false);

        var result = await new AssignmentTakingService(db).SubmitAsync(s.Assignment.Id, s.Student.Id, Text("work"));

        Assert.Equal(AssignmentError.NotEnrolled, result.Error);
        Assert.Equal(0, await db.Submissions.CountAsync());
    }

    [Fact]
    public async Task An_unpublished_assignment_is_NotFound()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, published: false);

        var result = await new AssignmentTakingService(db).SubmitAsync(s.Assignment.Id, s.Student.Id, Text("work"));

        Assert.Equal(AssignmentError.NotFound, result.Error);
    }

    [Fact]
    public async Task My_submission_is_NoSubmission_until_the_student_submits()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);

        var result = await new AssignmentTakingService(db).MySubmissionAsync(s.Assignment.Id, s.Student.Id);

        Assert.Equal(AssignmentError.NoSubmission, result.Error);
    }

    [Fact]
    public async Task The_status_moves_from_NotSubmitted_to_Submitted_to_Graded()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new AssignmentTakingService(db);

        Assert.Equal(SubmissionStatus.NotSubmitted, (await service.GetAsync(s.Assignment.Id, s.Student.Id, Roles.Student)).Value!.Status);

        await service.SubmitAsync(s.Assignment.Id, s.Student.Id, Text("work"));
        Assert.Equal(SubmissionStatus.Submitted, (await service.GetAsync(s.Assignment.Id, s.Student.Id, Roles.Student)).Value!.Status);

        var submission = await db.Submissions.SingleAsync();
        submission.Score = 90;
        submission.GradedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.Equal(SubmissionStatus.Graded, (await service.GetAsync(s.Assignment.Id, s.Student.Id, Roles.Student)).Value!.Status);
    }
}