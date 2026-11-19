namespace Lms.Api.Services;

public enum QuizError
{
    None,
    NotFound,                    // 404
    NotEnrolled,                 // 403
    NoAccess,                    // 403
    HasAttempts,                 // 409 quiz structure is locked / cannot be deleted
    NoAttemptsLeft,              // 409
    AlreadySubmitted,            // 409
    NotSubmitted,                // 409 results are not available yet
    NoQuestions,                 // 409 cannot publish an empty quiz
    LastQuestionOfPublishedQuiz, // 409
    InvalidModule,               // 400 the module is not in the quiz's course
    InvalidAnswers               // 400 the submitted answers do not match the quiz
}

public record QuizResult<T>(T? Value, QuizError Error, string? Detail = null) where T : class;

// Pure grading rules (no database), reused later by the xUnit tests.
public static class QuizGrader
{
    // "All or nothing": the selected options must be EXACTLY the correct options.
    public static bool IsQuestionCorrect(IEnumerable<int> correctOptionIds, IEnumerable<int> selectedOptionIds)
    {
        var correct = new HashSet<int>(correctOptionIds);
        return correct.Count > 0 && correct.SetEquals(selectedOptionIds);
    }

    // Rounded to the nearest whole percent (the number the student sees).
    public static int Percent(int earnedPoints, int totalPoints) =>
        totalPoints <= 0 ? 0 : (int)Math.Round(earnedPoints * 100.0 / totalPoints, MidpointRounding.AwayFromZero);

    public static bool IsPassed(int scorePercent, int passingScorePercent) => scorePercent >= passingScorePercent;
}