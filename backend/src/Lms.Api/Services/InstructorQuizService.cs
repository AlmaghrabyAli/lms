using Lms.Api.Data;
using Lms.Api.Dtos.Quizzes;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class InstructorQuizService(AppDbContext db) : IInstructorQuizService
{
    private const string LockedDetail =
        "Students have already started this quiz, so its questions, options and passing score can no longer change.";

    // ======================= Quizzes =======================

    public async Task<List<QuizSummaryDto>?> ListAsync(int courseId, int instructorId)
    {
        var owns = await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        if (!owns) return null;

        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(q => q.CourseId == courseId)
            .OrderBy(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.Title,
                q.Description,
                q.ModuleId,
                ModuleTitle = q.Module == null ? null : q.Module.Title,
                q.PassingScorePercent,
                q.MaxAttempts,
                q.IsPublished,
                QuestionCount = q.Questions.Count,
                TotalPoints = q.Questions.Sum(x => (int?)x.Points) ?? 0
            })
            .ToListAsync();

        var ids = quizzes.Select(q => q.Id).ToList();
        var attemptCounts = await db.QuizAttempts.AsNoTracking()
            .Where(a => ids.Contains(a.QuizId))
            .GroupBy(a => a.QuizId)
            .Select(g => new { QuizId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.QuizId, x => x.Count);

        return quizzes.Select(q => new QuizSummaryDto(
            q.Id, q.Title, q.Description, q.ModuleId, q.ModuleTitle, q.PassingScorePercent, q.MaxAttempts,
            q.IsPublished, q.QuestionCount, q.TotalPoints, attemptCounts.GetValueOrDefault(q.Id))).ToList();
    }

    public async Task<QuizResult<QuizDetailDto>> CreateAsync(int courseId, int instructorId, CreateQuizRequest request)
    {
        // Ownership: the course must belong to this instructor (someone else's course looks like a missing one).
        var owns = await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        if (!owns) return new QuizResult<QuizDetailDto>(null, QuizError.NotFound);

        if (request.ModuleId is not null &&
            !await db.Modules.AnyAsync(m => m.Id == request.ModuleId && m.CourseId == courseId))
            return new QuizResult<QuizDetailDto>(null, QuizError.InvalidModule);

        var quiz = new Quiz
        {
            CourseId = courseId,                       // from the URL, never from the body
            ModuleId = request.ModuleId,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            PassingScorePercent = request.PassingScorePercent,
            MaxAttempts = request.MaxAttempts
        };
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        return new QuizResult<QuizDetailDto>(await BuildDetailAsync(quiz.Id), QuizError.None);
    }

    public async Task<QuizResult<QuizDetailDto>> GetAsync(int quizId, int instructorId)
    {
        var owns = await db.Quizzes.AsNoTracking().AnyAsync(q => q.Id == quizId && q.Course.InstructorId == instructorId);
        if (!owns) return new QuizResult<QuizDetailDto>(null, QuizError.NotFound);
        return new QuizResult<QuizDetailDto>(await BuildDetailAsync(quizId), QuizError.None);
    }

    public async Task<QuizResult<QuizDetailDto>> UpdateAsync(int quizId, int instructorId, UpdateQuizRequest request)
    {
        var quiz = await OwnedQuizAsync(quizId, instructorId);
        if (quiz is null) return new QuizResult<QuizDetailDto>(null, QuizError.NotFound);

        if (request.ModuleId is not null &&
            !await db.Modules.AnyAsync(m => m.Id == request.ModuleId && m.CourseId == quiz.CourseId))
            return new QuizResult<QuizDetailDto>(null, QuizError.InvalidModule);

        // The passing score decides the stored "Passed" of past attempts, so it is frozen once attempts exist.
        var hasAttempts = await db.QuizAttempts.AnyAsync(a => a.QuizId == quizId);
        if (hasAttempts && request.PassingScorePercent != quiz.PassingScorePercent)
            return new QuizResult<QuizDetailDto>(null, QuizError.HasAttempts, LockedDetail);

        quiz.Title = request.Title.Trim();
        quiz.Description = Clean(request.Description);
        quiz.ModuleId = request.ModuleId;
        quiz.PassingScorePercent = request.PassingScorePercent;
        quiz.MaxAttempts = request.MaxAttempts;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new QuizResult<QuizDetailDto>(await BuildDetailAsync(quizId), QuizError.None);
    }

    public async Task<QuizError> DeleteAsync(int quizId, int instructorId)
    {
        var quiz = await OwnedQuizAsync(quizId, instructorId);
        if (quiz is null) return QuizError.NotFound;
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == quizId)) return QuizError.HasAttempts;

        db.Quizzes.Remove(quiz);   // the database cascade removes its questions and options
        await db.SaveChangesAsync();
        return QuizError.None;
    }

    public async Task<QuizResult<QuizDetailDto>> SetPublishedAsync(int quizId, int instructorId, bool publish)
    {
        var quiz = await OwnedQuizAsync(quizId, instructorId);
        if (quiz is null) return new QuizResult<QuizDetailDto>(null, QuizError.NotFound);

        if (publish && !await db.Questions.AnyAsync(q => q.QuizId == quizId))
            return new QuizResult<QuizDetailDto>(null, QuizError.NoQuestions);

        quiz.IsPublished = publish;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new QuizResult<QuizDetailDto>(await BuildDetailAsync(quizId), QuizError.None);
    }

    public async Task<QuizResult<List<QuizStudentResultDto>>> ListResultsAsync(int quizId, int instructorId)
    {
        var owns = await db.Quizzes.AsNoTracking().AnyAsync(q => q.Id == quizId && q.Course.InstructorId == instructorId);
        if (!owns) return new QuizResult<List<QuizStudentResultDto>>(null, QuizError.NotFound);

        // Submitted attempts only; grouped in memory (a quiz has at most a few hundred attempts at this scale).
        var rows = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.QuizId == quizId && a.SubmittedAt != null)
            .Select(a => new { a.UserId, a.ScorePercent, a.Passed, a.SubmittedAt })
            .ToListAsync();

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id);

        var list = rows
            .GroupBy(r => r.UserId)
            .Select(g => new QuizStudentResultDto(
                g.Key, users[g.Key].FullName, users[g.Key].Email, g.Count(),
                g.Max(x => x.ScorePercent ?? 0), g.Any(x => x.Passed == true), g.Max(x => x.SubmittedAt)))
            .OrderBy(x => x.FullName)
            .ToList();

        return new QuizResult<List<QuizStudentResultDto>>(list, QuizError.None);
    }

    // ======================= Questions =======================

    public async Task<QuizResult<QuestionDto>> AddQuestionAsync(int quizId, int instructorId, SaveQuestionRequest request)
    {
        var quiz = await OwnedQuizAsync(quizId, instructorId);
        if (quiz is null) return new QuizResult<QuestionDto>(null, QuizError.NotFound);
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == quizId))
            return new QuizResult<QuestionDto>(null, QuizError.HasAttempts, LockedDetail);

        var last = await db.Questions.Where(q => q.QuizId == quizId).MaxAsync(q => (int?)q.OrderIndex);
        var question = new Question
        {
            QuizId = quizId,
            Text = request.Text.Trim(),
            Type = request.Type!.Value,
            Points = request.Points,
            OrderIndex = (last ?? 0) + 1,
            Options = ToOptions(request)
        };
        db.Questions.Add(question);
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new QuizResult<QuestionDto>(ToQuestionDto(question), QuizError.None);
    }

    public async Task<QuizResult<QuestionDto>> UpdateQuestionAsync(int questionId, int instructorId, SaveQuestionRequest request)
    {
        var question = await db.Questions.Include(q => q.Options).Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.Quiz.Course.InstructorId == instructorId);
        if (question is null) return new QuizResult<QuestionDto>(null, QuizError.NotFound);
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == question.QuizId))
            return new QuizResult<QuestionDto>(null, QuizError.HasAttempts, LockedDetail);

        question.Text = request.Text.Trim();
        question.Type = request.Type!.Value;
        question.Points = request.Points;

        // Replace all options in ONE SaveChanges (atomic): old rows deleted, new rows inserted.
        db.AnswerOptions.RemoveRange(question.Options.ToList());
        foreach (var option in ToOptions(request))
        {
            option.QuestionId = question.Id;
            db.AnswerOptions.Add(option);
        }
        question.Quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var options = await db.AnswerOptions.AsNoTracking()
            .Where(o => o.QuestionId == questionId)
            .OrderBy(o => o.Id)
            .Select(o => new OptionDto(o.Id, o.Text, o.IsCorrect))
            .ToListAsync();

        return new QuizResult<QuestionDto>(
            new QuestionDto(question.Id, question.Text, question.Type, question.Points, question.OrderIndex, options),
            QuizError.None);
    }

    public async Task<QuizError> DeleteQuestionAsync(int questionId, int instructorId)
    {
        var question = await db.Questions.Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.Quiz.Course.InstructorId == instructorId);
        if (question is null) return QuizError.NotFound;
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == question.QuizId)) return QuizError.HasAttempts;

        // A published quiz must keep at least one question (same idea as "a published course keeps a lesson").
        if (question.Quiz.IsPublished &&
            !await db.Questions.AnyAsync(q => q.QuizId == question.QuizId && q.Id != questionId))
            return QuizError.LastQuestionOfPublishedQuiz;

        var siblings = await db.Questions
            .Where(q => q.QuizId == question.QuizId && q.Id != questionId)
            .OrderBy(q => q.OrderIndex).ThenBy(q => q.Id)
            .ToListAsync();

        db.Questions.Remove(question);   // the database cascade removes its options
        for (var i = 0; i < siblings.Count; i++) siblings[i].OrderIndex = i + 1;   // compact to 1..n
        question.Quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return QuizError.None;
    }

    // ======================= Helpers =======================

    private Task<Quiz?> OwnedQuizAsync(int quizId, int instructorId) =>
        db.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId && q.Course.InstructorId == instructorId);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static List<AnswerOption> ToOptions(SaveQuestionRequest request) =>
        request.Options!.Select(o => new AnswerOption { Text = o.Text.Trim(), IsCorrect = o.IsCorrect }).ToList();

    private static QuestionDto ToQuestionDto(Question q) => new(
        q.Id, q.Text, q.Type, q.Points, q.OrderIndex,
        q.Options.OrderBy(o => o.Id).Select(o => new OptionDto(o.Id, o.Text, o.IsCorrect)).ToList());

    private async Task<QuizDetailDto> BuildDetailAsync(int quizId)
    {
        var quiz = await db.Quizzes.AsNoTracking()
            .Where(q => q.Id == quizId)
            .Select(q => new { q.Id, q.CourseId, q.ModuleId, q.Title, q.Description, q.PassingScorePercent, q.MaxAttempts, q.IsPublished })
            .FirstAsync();

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.OrderIndex).ThenBy(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.Text,
                q.Type,
                q.Points,
                q.OrderIndex,
                Options = q.Options.OrderBy(o => o.Id).Select(o => new OptionDto(o.Id, o.Text, o.IsCorrect)).ToList()
            })
            .ToListAsync();

        var attempts = await db.QuizAttempts.AsNoTracking().CountAsync(a => a.QuizId == quizId);

        var dtos = questions.Select(q => new QuestionDto(q.Id, q.Text, q.Type, q.Points, q.OrderIndex, q.Options)).ToList();
        return new QuizDetailDto(
            quiz.Id, quiz.CourseId, quiz.ModuleId, quiz.Title, quiz.Description, quiz.PassingScorePercent,
            quiz.MaxAttempts, quiz.IsPublished, attempts > 0, attempts, dtos.Sum(x => x.Points), dtos);
    }
}