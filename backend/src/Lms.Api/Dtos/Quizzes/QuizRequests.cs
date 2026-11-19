using System.ComponentModel.DataAnnotations;
using Lms.Api.Entities;

namespace Lms.Api.Dtos.Quizzes;

public class CreateQuizRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public int? ModuleId { get; set; }

    [Range(1, 100)]
    public int PassingScorePercent { get; set; } = 60;

    [Range(1, 20)]
    public int? MaxAttempts { get; set; }
}

// Empty on purpose: update and create can get different rules later.
public class UpdateQuizRequest : CreateQuizRequest
{
}

public class OptionRequest
{
    [Required, StringLength(500, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }
}

public class SaveQuestionRequest : IValidatableObject
{
    [Required, StringLength(1000, MinimumLength = 2)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public QuestionType? Type { get; set; }

    [Range(1, 100)]
    public int Points { get; set; } = 1;

    [Required]
    public List<OptionRequest>? Options { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type is null || Options is null) yield break;

        if (Options.Any(o => o is null))
        {
            yield return new ValidationResult("Options cannot contain empty items.", new[] { nameof(Options) });
            yield break;
        }

        var count = Options.Count;
        var correct = Options.Count(o => o.IsCorrect);

        if (count < 2 || count > 8)
            yield return new ValidationResult("A question needs between 2 and 8 options.", new[] { nameof(Options) });
        if (correct == 0)
            yield return new ValidationResult("Mark at least one option as correct.", new[] { nameof(Options) });
        if (count >= 2 && correct == count)
            yield return new ValidationResult("At least one option must be wrong.", new[] { nameof(Options) });
        if ((Type == QuestionType.SingleChoice || Type == QuestionType.TrueFalse) && correct > 1)
            yield return new ValidationResult("This question type allows exactly one correct option.", new[] { nameof(Options) });
        if (Type == QuestionType.TrueFalse && count != 2)
            yield return new ValidationResult("A True/False question has exactly 2 options.", new[] { nameof(Options) });

        var distinct = Options.Select(o => (o.Text ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count();
        if (distinct != count)
            yield return new ValidationResult("Options must be different from each other.", new[] { nameof(Options) });
    }
}

public class AnswerRequest
{
    public int QuestionId { get; set; }
    public List<int>? SelectedOptionIds { get; set; }
}

public class SubmitAttemptRequest
{
    [Required, MaxLength(200)]
    public List<AnswerRequest>? Answers { get; set; }
}