using Lms.Api.Dtos.Quizzes;
using Lms.Api.Entities;
using Lms.Api.Services;
using Lms.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lms.Tests.Services;

public class InstructorQuizServiceTests
{
    private static SaveQuestionRequest Question(string text = "Question?") => new()
    {
        Text = text,
        Type = QuestionType.SingleChoice,
        Points = 1,
        Options = new List<OptionRequest> { new() { Text = "Yes", IsCorrect = true }, new() { Text = "No" } }
    };

    private static CreateQuizRequest NewQuiz(string title = "Quiz one", int passing = 60) => new() { Title = title, PassingScorePercent = passing };

    [Fact]
    public async Task Another_instructor_cannot_touch_the_quiz_and_it_looks_missing()
    {
        await using var db = TestDb.Create();
        var owner = await TestDb.AddUserAsync(db, Roles.Instructor);
        var other = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, owner);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, owner.Id, NewQuiz())).Value!.Id;

        Assert.Null(await service.ListAsync(course.Id, other.Id));
        Assert.Equal(QuizError.NotFound, (await service.GetAsync(quizId, other.Id)).Error);
        Assert.Equal(QuizError.NotFound, (await service.UpdateAsync(quizId, other.Id, new UpdateQuizRequest { Title = "Hacked", PassingScorePercent = 60 })).Error);
        Assert.Equal(QuizError.NotFound, await service.DeleteAsync(quizId, other.Id));
        Assert.Equal(QuizError.NotFound, (await service.SetPublishedAsync(quizId, other.Id, true)).Error);
        Assert.Equal(QuizError.NotFound, (await service.AddQuestionAsync(quizId, other.Id, Question())).Error);
    }

    [Fact]
    public async Task A_quiz_cannot_be_attached_to_a_module_of_another_course()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var courseA = await TestDb.AddCourseAsync(db, instructor);
        var courseB = await TestDb.AddCourseAsync(db, instructor);
        var moduleOfB = await db.Modules.FirstAsync(m => m.CourseId == courseB.Id);

        var result = await new InstructorQuizService(db).CreateAsync(courseA.Id, instructor.Id,
            new CreateQuizRequest { Title = "Quiz", PassingScorePercent = 60, ModuleId = moduleOfB.Id });

        Assert.Equal(QuizError.InvalidModule, result.Error);
    }

    [Fact]
    public async Task A_quiz_without_questions_cannot_be_published()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, instructor);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, instructor.Id, NewQuiz())).Value!.Id;

        Assert.Equal(QuizError.NoQuestions, (await service.SetPublishedAsync(quizId, instructor.Id, true)).Error);

        await service.AddQuestionAsync(quizId, instructor.Id, Question());
        var published = await service.SetPublishedAsync(quizId, instructor.Id, true);

        Assert.Equal(QuizError.None, published.Error);
        Assert.True(published.Value!.IsPublished);
    }

    [Fact]
    public async Task Once_students_have_attempts_the_structure_is_locked()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var course = await TestDb.AddCourseAsync(db, instructor);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, instructor.Id, NewQuiz())).Value!.Id;
        var questionId = (await service.AddQuestionAsync(quizId, instructor.Id, Question())).Value!.Id;

        db.QuizAttempts.Add(new QuizAttempt { QuizId = quizId, UserId = student.Id });
        await db.SaveChangesAsync();

        Assert.Equal(QuizError.HasAttempts, (await service.AddQuestionAsync(quizId, instructor.Id, Question("Another?"))).Error);
        Assert.Equal(QuizError.HasAttempts, (await service.UpdateQuestionAsync(questionId, instructor.Id, Question("Edited?"))).Error);
        Assert.Equal(QuizError.HasAttempts, await service.DeleteQuestionAsync(questionId, instructor.Id));
        Assert.Equal(QuizError.HasAttempts, await service.DeleteAsync(quizId, instructor.Id));
        Assert.Equal(1, await db.Quizzes.CountAsync());
    }

    [Fact]
    public async Task A_locked_quiz_can_be_renamed_but_its_passing_score_is_frozen()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var student = await TestDb.AddUserAsync(db, Roles.Student);
        var course = await TestDb.AddCourseAsync(db, instructor);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, instructor.Id, NewQuiz(passing: 60))).Value!.Id;
        db.QuizAttempts.Add(new QuizAttempt { QuizId = quizId, UserId = student.Id });
        await db.SaveChangesAsync();

        var renamed = await service.UpdateAsync(quizId, instructor.Id, new UpdateQuizRequest { Title = "Renamed", PassingScorePercent = 60 });
        var changed = await service.UpdateAsync(quizId, instructor.Id, new UpdateQuizRequest { Title = "Renamed", PassingScorePercent = 70 });

        Assert.Equal(QuizError.None, renamed.Error);
        Assert.Equal("Renamed", renamed.Value!.Title);
        Assert.Equal(QuizError.HasAttempts, changed.Error);
    }

    [Fact]
    public async Task A_published_quiz_keeps_at_least_one_question()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, instructor);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, instructor.Id, NewQuiz())).Value!.Id;
        var questionId = (await service.AddQuestionAsync(quizId, instructor.Id, Question())).Value!.Id;
        await service.SetPublishedAsync(quizId, instructor.Id, true);

        var result = await service.DeleteQuestionAsync(questionId, instructor.Id);

        Assert.Equal(QuizError.LastQuestionOfPublishedQuiz, result);
        Assert.Equal(1, await db.Questions.CountAsync());
    }

    [Fact]
    public async Task Deleting_a_question_compacts_the_order()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, instructor);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, instructor.Id, NewQuiz())).Value!.Id;
        var first = (await service.AddQuestionAsync(quizId, instructor.Id, Question("One?"))).Value!.Id;
        await service.AddQuestionAsync(quizId, instructor.Id, Question("Two?"));
        await service.AddQuestionAsync(quizId, instructor.Id, Question("Three?"));

        var error = await service.DeleteQuestionAsync(first, instructor.Id);

        Assert.Equal(QuizError.None, error);
        var orders = await db.Questions.OrderBy(q => q.OrderIndex).Select(q => q.OrderIndex).ToListAsync();
        Assert.Equal(new List<int> { 1, 2 }, orders);
    }

    [Fact]
    public async Task Updating_a_question_replaces_all_of_its_options()
    {
        await using var db = TestDb.Create();
        var instructor = await TestDb.AddUserAsync(db, Roles.Instructor);
        var course = await TestDb.AddCourseAsync(db, instructor);
        var service = new InstructorQuizService(db);
        var quizId = (await service.CreateAsync(course.Id, instructor.Id, NewQuiz())).Value!.Id;
        var questionId = (await service.AddQuestionAsync(quizId, instructor.Id, Question())).Value!.Id;

        var update = new SaveQuestionRequest
        {
            Text = "Edited?",
            Type = QuestionType.SingleChoice,
            Points = 2,
            Options = new List<OptionRequest> { new() { Text = "X", IsCorrect = true }, new() { Text = "Y" }, new() { Text = "Z" } }
        };
        var result = await service.UpdateQuestionAsync(questionId, instructor.Id, update);

        Assert.Equal(QuizError.None, result.Error);
        Assert.Equal(3, result.Value!.Options.Count);
        Assert.Equal(2, result.Value.Points);
        Assert.Equal(3, await db.AnswerOptions.CountAsync(o => o.QuestionId == questionId));
    }
}