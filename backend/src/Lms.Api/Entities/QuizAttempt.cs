namespace Lms.Api.Entities;

public class QuizAttempt
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public int? ScorePercent { get; set; }
    public bool? Passed { get; set; }
    public ICollection<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();
}

// One row per selected option (a MultipleChoice answer has several rows).
public class AttemptAnswer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public QuizAttempt Attempt { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int SelectedOptionId { get; set; }
    public AnswerOption SelectedOption { get; set; } = null!;
}