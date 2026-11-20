using Lms.Api.Data;
using Lms.Api.Dtos.Quizzes;
using Lms.Api.Entities;
using Lms.Api.Services;
using Lms.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lms.Tests.Services;

public class QuizTakingServiceTests
{
    private sealed record Setup(User Instructor, User Student, Course Course, Quiz Quiz, int SingleId, int MultipleId, int Right, int Wrong, int A, int B, int C);

    private static async Task<Setup> ArrangeAsync(AppDbContext db, int? maxAttempts = null, bool quizPublished = true, bool enroll = true, bool coursePublished = true)
    {
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var course = await TestDb.AddCourseAsync(db, instructor, coursePublished);
        if (enroll) await TestDb.EnrollAsync(db, student, course);
        var quiz = await TestDb.AddQuizAsync(db, course, quizPublished, 60, maxAttempts);

        var questions = await db.Questions.Where(q => q.QuizId == quiz.Id).ToListAsync();
        var options = await db.AnswerOptions.ToListAsync();
        int Q(string text) => questions.First(q => q.Text == text).Id;
        int O(string question, string option) => options.First(o => o.QuestionId == Q(question) && o.Text == option).Id;

        return new Setup(instructor, student, course, quiz, Q("Single"), Q("Multiple"),
            O("Single", "Right"), O("Single", "Wrong"), O("Multiple", "A"), O("Multiple", "B"), O("Multiple", "C"));
    }

    private static SubmitAttemptRequest Answers(params (int QuestionId, int[] OptionIds)[] answers) => new()
    {
        Answers = answers.Select(a => new AnswerRequest { QuestionId = a.QuestionId, SelectedOptionIds = a.OptionIds.ToList() }).ToList()
    };

    private static async Task<int> StartAsync(QuizTakingService service, Setup s) =>
        (await service.StartAttemptAsync(s.Quiz.Id, s.Student.Id)).Value!.AttemptId;

    [Fact]
    public async Task A_student_who_is_not_enrolled_gets_NotEnrolled()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, enroll: false);

        var result = await new QuizTakingService(db).GetAsync(s.Quiz.Id, s.Student.Id, Roles.Student);

        Assert.Equal(QuizError.NotEnrolled, result.Error);
    }

    [Fact]
    public async Task An_unpublished_quiz_is_NotFound_even_for_an_enrolled_student()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, quizPublished: false);

        var result = await new QuizTakingService(db).GetAsync(s.Quiz.Id, s.Student.Id, Roles.Student);

        Assert.Equal(QuizError.NotFound, result.Error);
    }

    [Fact]
    public async Task A_quiz_of_a_draft_course_is_NotFound()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, coursePublished: false);

        var result = await new QuizTakingService(db).GetAsync(s.Quiz.Id, s.Student.Id, Roles.Student);

        Assert.Equal(QuizError.NotFound, result.Error);
    }

    [Fact]
    public async Task Only_the_course_owner_and_admins_may_preview()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var other = await TestDb.AddUserAsync(db, Roles.Instructor);
        var service = new QuizTakingService(db);

        Assert.Equal(QuizError.NoAccess, (await service.GetAsync(s.Quiz.Id, other.Id, Roles.Instructor)).Error);
        Assert.Equal(QuizError.None, (await service.GetAsync(s.Quiz.Id, s.Instructor.Id, Roles.Instructor)).Error);
        Assert.Equal(QuizError.None, (await service.GetAsync(s.Quiz.Id, 99999, Roles.Admin)).Error);
    }

    [Fact]
    public async Task Starting_twice_returns_the_same_open_attempt()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);

        var first = await service.StartAttemptAsync(s.Quiz.Id, s.Student.Id);
        var second = await service.StartAttemptAsync(s.Quiz.Id, s.Student.Id);

        Assert.False(first.Value!.Resumed);
        Assert.True(second.Value!.Resumed);
        Assert.Equal(first.Value.AttemptId, second.Value.AttemptId);
        Assert.Equal(1, await db.QuizAttempts.CountAsync());
    }

    [Fact]
    public void The_student_types_have_no_field_that_could_carry_a_correct_answer()
    {
        Assert.Null(typeof(StudentOptionDto).GetProperty("IsCorrect"));
        Assert.Null(typeof(StudentQuestionDto).GetProperty("IsCorrect"));
        Assert.Null(typeof(AttemptStartDto).GetProperty("IsCorrect"));
        Assert.Null(typeof(QuizListItemDto).GetProperty("Questions"));
    }

    [Fact]
    public async Task MaxAttempts_is_enforced()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db, maxAttempts: 1);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var submitted = await service.SubmitAsync(attemptId, s.Student.Id, new SubmitAttemptRequest { Answers = new List<AnswerRequest>() });
        var again = await service.StartAttemptAsync(s.Quiz.Id, s.Student.Id);

        Assert.Equal(QuizError.None, submitted.Error);
        Assert.Equal(QuizError.NoAttemptsLeft, again.Error);
    }

    [Fact]
    public async Task Every_question_right_scores_100_and_passes()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var result = await service.SubmitAsync(attemptId, s.Student.Id,
            Answers((s.SingleId, new[] { s.Right }), (s.MultipleId, new[] { s.A, s.B })));

        Assert.Equal(QuizError.None, result.Error);
        Assert.Equal(100, result.Value!.ScorePercent);
        Assert.True(result.Value.Passed);
        Assert.Equal(4, result.Value.EarnedPoints);
        Assert.Equal(4, result.Value.TotalPoints);
    }

    [Fact]
    public async Task Unanswered_questions_count_as_wrong()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var result = await service.SubmitAsync(attemptId, s.Student.Id, Answers((s.SingleId, new[] { s.Right })));

        Assert.Equal(25, result.Value!.ScorePercent);
        Assert.False(result.Value.Passed);
        Assert.Equal(1, result.Value.EarnedPoints);
    }

    [Fact]
    public async Task A_multiple_choice_with_only_some_correct_options_earns_nothing()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var result = await service.SubmitAsync(attemptId, s.Student.Id,
            Answers((s.SingleId, new[] { s.Right }), (s.MultipleId, new[] { s.A })));

        Assert.Equal(25, result.Value!.ScorePercent);
        Assert.False(result.Value.Questions[1].IsCorrect);
    }

    [Fact]
    public async Task A_multiple_choice_with_an_extra_wrong_option_earns_nothing()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var result = await service.SubmitAsync(attemptId, s.Student.Id,
            Answers((s.SingleId, new[] { s.Right }), (s.MultipleId, new[] { s.A, s.B, s.C })));

        Assert.Equal(25, result.Value!.ScorePercent);
        Assert.False(result.Value.Questions[1].IsCorrect);
    }

    [Fact]
    public async Task An_option_of_another_question_is_rejected_and_the_attempt_stays_open()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var result = await service.SubmitAsync(attemptId, s.Student.Id, Answers((s.SingleId, new[] { s.A })));

        Assert.Equal(QuizError.InvalidAnswers, result.Error);
        Assert.Null((await db.QuizAttempts.SingleAsync()).SubmittedAt);
    }

    [Fact]
    public async Task Answering_the_same_question_twice_is_rejected()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        var result = await service.SubmitAsync(attemptId, s.Student.Id,
            Answers((s.SingleId, new[] { s.Right }), (s.SingleId, new[] { s.Wrong })));

        Assert.Equal(QuizError.InvalidAnswers, result.Error);
    }

    [Fact]
    public async Task Another_students_attempt_looks_like_a_missing_attempt()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var other = await TestDb.AddUserAsync(db, Roles.Student);
        await TestDb.EnrollAsync(db, other, s.Course);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        Assert.Equal(QuizError.NotFound, (await service.SubmitAsync(attemptId, other.Id, Answers())).Error);
        Assert.Equal(QuizError.NotFound, (await service.GetResultAsync(attemptId, other.Id)).Error);
    }

    [Fact]
    public async Task The_result_is_hidden_until_submit_and_then_reveals_the_correct_answers()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        Assert.Equal(QuizError.NotSubmitted, (await service.GetResultAsync(attemptId, s.Student.Id)).Error);

        await service.SubmitAsync(attemptId, s.Student.Id, Answers((s.SingleId, new[] { s.Right })));
        var result = await service.GetResultAsync(attemptId, s.Student.Id);

        Assert.Equal(QuizError.None, result.Error);
        var options = result.Value!.Questions.SelectMany(q => q.Options).ToList();
        Assert.Equal(3, options.Count(o => o.IsCorrect));
        Assert.Single(options, o => o.WasSelected);
    }

    [Fact]
    public async Task A_submitted_attempt_cannot_be_submitted_again()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);
        var attemptId = await StartAsync(service, s);

        await service.SubmitAsync(attemptId, s.Student.Id, Answers());
        var again = await service.SubmitAsync(attemptId, s.Student.Id, Answers((s.SingleId, new[] { s.Right })));

        Assert.Equal(QuizError.AlreadySubmitted, again.Error);
    }

    [Fact]
    public async Task The_best_score_is_kept_across_attempts()
    {
        await using var db = TestDb.Create();
        var s = await ArrangeAsync(db);
        var service = new QuizTakingService(db);

        var first = await StartAsync(service, s);
        await service.SubmitAsync(first, s.Student.Id, Answers((s.SingleId, new[] { s.Right })));
        var second = await StartAsync(service, s);
        await service.SubmitAsync(second, s.Student.Id,
            Answers((s.SingleId, new[] { s.Right }), (s.MultipleId, new[] { s.A, s.B })));

        var info = (await service.GetAsync(s.Quiz.Id, s.Student.Id, Roles.Student)).Value!;

        Assert.Equal(2, info.AttemptsUsed);
        Assert.Equal(100, info.BestScorePercent);
        Assert.True(info.Passed);
        Assert.Null(info.InProgressAttemptId);
    }
}