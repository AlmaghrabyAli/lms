using Lms.Api.Data;
using Lms.Api.Dtos.Quizzes;
using Lms.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class QuizTakingService(AppDbContext db) : IQuizTakingService
{
    private sealed record Access(QuizError Error, bool IsStudent);
    private sealed record QuizInfo(int Id, int CourseId, string Title, string? Description, int? ModuleId, int PassingScorePercent, int? MaxAttempts);
    private sealed record AttemptRow(int Id, int QuizId, DateTime StartedAt, DateTime? SubmittedAt, int? ScorePercent, bool? Passed);

    // ======================= Read =======================

    public async Task<QuizResult<List<QuizListItemDto>>> ListForCourseAsync(int courseId, int userId, string role)
    {
        var access = await ResolveAccessAsync(courseId, userId, role);
        if (access.Error != QuizError.None) return new QuizResult<List<QuizListItemDto>>(null, access.Error);

        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(q => q.CourseId == courseId && q.IsPublished)
            .OrderBy(q => q.Id)
            .Select(q => new QuizInfo(q.Id, q.CourseId, q.Title, q.Description, q.ModuleId, q.PassingScorePercent, q.MaxAttempts))
            .ToListAsync();

        return new QuizResult<List<QuizListItemDto>>(await BuildItemsAsync(quizzes, userId, access.IsStudent), QuizError.None);
    }

    public async Task<QuizResult<QuizListItemDto>> GetAsync(int quizId, int userId, string role)
    {
        var gate = await GateAsync(quizId, userId, role);
        if (gate.Error != QuizError.None) return new QuizResult<QuizListItemDto>(null, gate.Error);

        var items = await BuildItemsAsync(new List<QuizInfo> { gate.Quiz! }, userId, gate.IsStudent);
        return new QuizResult<QuizListItemDto>(items[0], QuizError.None);
    }

    public async Task<QuizResult<MyAttemptsDto>> MyAttemptsAsync(int quizId, int userId)
    {
        var gate = await GateAsync(quizId, userId, Roles.Student);
        if (gate.Error != QuizError.None) return new QuizResult<MyAttemptsDto>(null, gate.Error);

        var rows = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.QuizId == quizId && a.UserId == userId)
            .OrderByDescending(a => a.StartedAt).ThenByDescending(a => a.Id)
            .Select(a => new AttemptSummaryDto(a.Id, a.StartedAt, a.SubmittedAt, a.ScorePercent, a.Passed))
            .ToListAsync();

        var scores = rows.Where(a => a.ScorePercent != null).Select(a => a.ScorePercent!.Value).ToList();
        int? best = scores.Count == 0 ? null : scores.Max();
        var max = gate.Quiz!.MaxAttempts;
        int? left = max is null ? null : Math.Max(max.Value - rows.Count, 0);

        return new QuizResult<MyAttemptsDto>(new MyAttemptsDto(quizId, rows.Count, max, left, best, rows), QuizError.None);
    }

    // ======================= Take a quiz =======================

    public async Task<QuizResult<AttemptStartDto>> StartAttemptAsync(int quizId, int userId)
    {
        var gate = await GateAsync(quizId, userId, Roles.Student);
        if (gate.Error != QuizError.None) return new QuizResult<AttemptStartDto>(null, gate.Error);
        var quiz = gate.Quiz!;

        // A refresh must not burn an attempt: an open attempt is simply returned again.
        var attempt = await db.QuizAttempts.FirstOrDefaultAsync(a => a.QuizId == quizId && a.UserId == userId && a.SubmittedAt == null);
        var resumed = attempt is not null;

        if (attempt is null)
        {
            var used = await db.QuizAttempts.CountAsync(a => a.QuizId == quizId && a.UserId == userId);
            if (quiz.MaxAttempts is not null && used >= quiz.MaxAttempts)
                return new QuizResult<AttemptStartDto>(null, QuizError.NoAttemptsLeft);

            attempt = new QuizAttempt { QuizId = quizId, UserId = userId };
            db.QuizAttempts.Add(attempt);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Two quick clicks: the filtered unique index let only one open attempt in. Reuse it.
                db.Entry(attempt).State = EntityState.Detached;
                attempt = await db.QuizAttempts.FirstAsync(a => a.QuizId == quizId && a.UserId == userId && a.SubmittedAt == null);
                resumed = true;
            }
        }

        var questions = await LoadStudentQuestionsAsync(quizId);
        return new QuizResult<AttemptStartDto>(
            new AttemptStartDto(attempt.Id, quizId, quiz.Title, quiz.PassingScorePercent,
                questions.Sum(q => q.Points), attempt.StartedAt, resumed, questions),
            QuizError.None);
    }

    public async Task<QuizResult<AttemptResultDto>> SubmitAsync(int attemptId, int userId, SubmitAttemptRequest request)
    {
        // The attempt must be MINE (another student's attempt looks like a missing one).
        var attempt = await db.QuizAttempts.Include(a => a.Quiz)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
        if (attempt is null) return new QuizResult<AttemptResultDto>(null, QuizError.NotFound);
        if (attempt.SubmittedAt is not null) return new QuizResult<AttemptResultDto>(null, QuizError.AlreadySubmitted);

        // The quiz must still be published and the course still reachable.
        var gate = await GateAsync(attempt.QuizId, userId, Roles.Student);
        if (gate.Error != QuizError.None) return new QuizResult<AttemptResultDto>(null, gate.Error);

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.QuizId == attempt.QuizId)
            .Select(q => new { q.Id, q.Type, q.Points, Options = q.Options.Select(o => new { o.Id, o.IsCorrect }).ToList() })
            .ToListAsync();

        // ---- validate what the client sent (never trust it) ----
        var answers = request.Answers ?? new List<AnswerRequest>();
        if (answers.Any(a => a is null)) return Invalid("Answers cannot contain empty items.");
        if (answers.Select(a => a.QuestionId).Distinct().Count() != answers.Count)
            return Invalid("Each question can be answered only once.");

        var selection = new Dictionary<int, List<int>>();
        foreach (var answer in answers)
        {
            var question = questions.FirstOrDefault(x => x.Id == answer.QuestionId);
            if (question is null) return Invalid($"Question {answer.QuestionId} does not belong to this quiz.");

            var picked = answer.SelectedOptionIds ?? new List<int>();
            if (picked.Distinct().Count() != picked.Count) return Invalid("An option can be selected only once.");
            if (picked.Any(id => question.Options.All(o => o.Id != id)))
                return Invalid($"An option does not belong to question {answer.QuestionId}.");
            if (question.Type != QuestionType.MultipleChoice && picked.Count > 1)
                return Invalid($"Question {answer.QuestionId} accepts only one option.");

            selection[answer.QuestionId] = picked;
        }

        // ---- grade on the server ----
        var total = questions.Sum(x => x.Points);
        var earned = 0;
        foreach (var item in questions)
        {
            var picked = selection.GetValueOrDefault(item.Id) ?? new List<int>();
            var correctIds = item.Options.Where(o => o.IsCorrect).Select(o => o.Id);
            if (QuizGrader.IsQuestionCorrect(correctIds, picked)) earned += item.Points;
        }
        var score = QuizGrader.Percent(earned, total);

        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.ScorePercent = score;
        attempt.Passed = QuizGrader.IsPassed(score, attempt.Quiz.PassingScorePercent);
        foreach (var pair in selection)
            foreach (var optionId in pair.Value)
                db.AttemptAnswers.Add(new AttemptAnswer { AttemptId = attempt.Id, QuestionId = pair.Key, SelectedOptionId = optionId });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A double submit raced us: the unique (AttemptId, SelectedOptionId) index let only one through.
            return new QuizResult<AttemptResultDto>(null, QuizError.AlreadySubmitted);
        }

        return await BuildResultAsync(attemptId);
    }

    public async Task<QuizResult<AttemptResultDto>> GetResultAsync(int attemptId, int userId)
    {
        var owned = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.Id == attemptId && a.UserId == userId)
            .Select(a => new { a.SubmittedAt })
            .FirstOrDefaultAsync();
        if (owned is null) return new QuizResult<AttemptResultDto>(null, QuizError.NotFound);

        // Correct answers are revealed only AFTER submission.
        if (owned.SubmittedAt is null) return new QuizResult<AttemptResultDto>(null, QuizError.NotSubmitted);

        return await BuildResultAsync(attemptId);
    }

    // ======================= Helpers =======================

    private static QuizResult<AttemptResultDto> Invalid(string detail) =>
        new(null, QuizError.InvalidAnswers, detail);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    // Same resource-based rule as the learn view: the course must be published (else 404), then the role decides.
    private async Task<Access> ResolveAccessAsync(int courseId, int userId, string role)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.IsPublished)
            .Select(c => new { c.InstructorId })
            .FirstOrDefaultAsync();
        if (course is null) return new Access(QuizError.NotFound, false);

        switch (role)
        {
            case Roles.Student:
                var enrolled = await db.Enrollments.AsNoTracking().AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
                return enrolled ? new Access(QuizError.None, true) : new Access(QuizError.NotEnrolled, false);
            case Roles.Instructor:
                return course.InstructorId == userId ? new Access(QuizError.None, false) : new Access(QuizError.NoAccess, false);
            case Roles.Admin:
                return new Access(QuizError.None, false);
            default:
                return new Access(QuizError.NoAccess, false);
        }
    }

    // quiz -> must exist AND be published (else 404), then the course access rule above.
    private async Task<(QuizError Error, QuizInfo? Quiz, bool IsStudent)> GateAsync(int quizId, int userId, string role)
    {
        var quiz = await db.Quizzes.AsNoTracking()
            .Where(q => q.Id == quizId && q.IsPublished)
            .Select(q => new QuizInfo(q.Id, q.CourseId, q.Title, q.Description, q.ModuleId, q.PassingScorePercent, q.MaxAttempts))
            .FirstOrDefaultAsync();
        if (quiz is null) return (QuizError.NotFound, null, false);

        var access = await ResolveAccessAsync(quiz.CourseId, userId, role);
        return (access.Error, access.Error == QuizError.None ? quiz : null, access.IsStudent);
    }

    private async Task<List<QuizListItemDto>> BuildItemsAsync(List<QuizInfo> quizzes, int userId, bool isStudent)
    {
        var ids = quizzes.Select(q => q.Id).ToList();

        var stats = await db.Questions.AsNoTracking()
            .Where(q => ids.Contains(q.QuizId))
            .GroupBy(q => q.QuizId)
            .Select(g => new { QuizId = g.Key, Count = g.Count(), Points = g.Sum(q => q.Points) })
            .ToDictionaryAsync(x => x.QuizId);

        // A student's own attempts for these quizzes are few, so they are grouped in memory.
        var attempts = new List<AttemptRow>();
        if (isStudent)
        {
            attempts = await db.QuizAttempts.AsNoTracking()
                .Where(a => a.UserId == userId && ids.Contains(a.QuizId))
                .Select(a => new AttemptRow(a.Id, a.QuizId, a.StartedAt, a.SubmittedAt, a.ScorePercent, a.Passed))
                .ToListAsync();
        }

        return quizzes.Select(q =>
        {
            var s = stats.GetValueOrDefault(q.Id);
            var mine = attempts.Where(a => a.QuizId == q.Id).ToList();
            var submitted = mine.Where(a => a.SubmittedAt != null).ToList();
            int? best = submitted.Count == 0 ? null : submitted.Max(a => a.ScorePercent ?? 0);
            int? left = q.MaxAttempts is null ? null : Math.Max(q.MaxAttempts.Value - mine.Count, 0);
            var open = mine.FirstOrDefault(a => a.SubmittedAt == null);
            return new QuizListItemDto(
                q.Id, q.CourseId, q.Title, q.Description, q.ModuleId,
                s?.Count ?? 0, s?.Points ?? 0, q.PassingScorePercent, q.MaxAttempts,
                mine.Count, left, best, submitted.Any(a => a.Passed == true), open?.Id);
        }).ToList();
    }

    // The projection never reads IsCorrect: the correct answers do not even leave the database here.
    private async Task<List<StudentQuestionDto>> LoadStudentQuestionsAsync(int quizId)
    {
        var rows = await db.Questions.AsNoTracking()
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.OrderIndex).ThenBy(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.Text,
                q.Type,
                q.Points,
                q.OrderIndex,
                Options = q.Options.OrderBy(o => o.Id).Select(o => new StudentOptionDto(o.Id, o.Text)).ToList()
            })
            .ToListAsync();

        return rows.Select(q => new StudentQuestionDto(q.Id, q.Text, q.Type, q.Points, q.OrderIndex, q.Options)).ToList();
    }

    private async Task<QuizResult<AttemptResultDto>> BuildResultAsync(int attemptId)
    {
        var attempt = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.Id == attemptId)
            .Select(a => new
            {
                a.Id,
                a.QuizId,
                QuizTitle = a.Quiz.Title,
                a.Quiz.CourseId,
                Passing = a.Quiz.PassingScorePercent,
                a.StartedAt,
                a.SubmittedAt,
                a.ScorePercent,
                a.Passed
            })
            .FirstAsync();

        var chosen = (await db.AttemptAnswers.AsNoTracking()
            .Where(x => x.AttemptId == attemptId)
            .Select(x => x.SelectedOptionId)
            .ToListAsync()).ToHashSet();

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.QuizId == attempt.QuizId)
            .OrderBy(q => q.OrderIndex).ThenBy(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.Text,
                q.Type,
                q.Points,
                q.OrderIndex,
                Options = q.Options.OrderBy(o => o.Id).Select(o => new { o.Id, o.Text, o.IsCorrect }).ToList()
            })
            .ToListAsync();

        var resultQuestions = questions.Select(q =>
        {
            var correctIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id);
            var selectedIds = q.Options.Where(o => chosen.Contains(o.Id)).Select(o => o.Id);
            var isCorrect = QuizGrader.IsQuestionCorrect(correctIds, selectedIds);
            return new ResultQuestionDto(
                q.Id, q.Text, q.Type, q.Points, q.OrderIndex, isCorrect, isCorrect ? q.Points : 0,
                q.Options.Select(o => new ResultOptionDto(o.Id, o.Text, o.IsCorrect, chosen.Contains(o.Id))).ToList());
        }).ToList();

        return new QuizResult<AttemptResultDto>(
            new AttemptResultDto(
                attempt.Id, attempt.QuizId, attempt.QuizTitle, attempt.CourseId, attempt.StartedAt,
                attempt.SubmittedAt ?? DateTime.UtcNow, attempt.ScorePercent ?? 0, attempt.Passed ?? false, attempt.Passing,
                resultQuestions.Sum(x => x.EarnedPoints), resultQuestions.Sum(x => x.Points), resultQuestions),
            QuizError.None);
    }
}