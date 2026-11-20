using System.ComponentModel.DataAnnotations;
using Lms.Api.Dtos.Admin;
using Lms.Api.Dtos.Assignments;
using Lms.Api.Dtos.Quizzes;
using Lms.Api.Entities;
using Xunit;

namespace Lms.Tests.Dtos;

public class RequestValidationTests
{
    private static List<OptionRequest> Options(string[] texts, params int[] correct) =>
        texts.Select((t, i) => new OptionRequest { Text = t, IsCorrect = correct.Contains(i) }).ToList();

    private static List<ValidationResult> Run(SaveQuestionRequest r) => r.Validate(new ValidationContext(r)).ToList();

    private static SaveQuestionRequest Question(QuestionType type, string[] texts, params int[] correct) =>
        new() { Text = "Question?", Type = type, Points = 1, Options = Options(texts, correct) };

    [Fact]
    public void A_valid_single_choice_question_has_no_errors() =>
        Assert.Empty(Run(Question(QuestionType.SingleChoice, new[] { "A", "B", "C" }, 1)));

    [Fact]
    public void A_single_choice_with_two_correct_options_is_invalid() =>
        Assert.NotEmpty(Run(Question(QuestionType.SingleChoice, new[] { "A", "B", "C" }, 0, 1)));

    [Fact]
    public void A_question_without_a_correct_option_is_invalid() =>
        Assert.NotEmpty(Run(Question(QuestionType.SingleChoice, new[] { "A", "B" })));

    [Fact]
    public void A_multiple_choice_where_every_option_is_correct_is_invalid() =>
        Assert.NotEmpty(Run(Question(QuestionType.MultipleChoice, new[] { "A", "B", "C" }, 0, 1, 2)));

    [Fact]
    public void A_true_false_question_needs_exactly_two_options() =>
        Assert.NotEmpty(Run(Question(QuestionType.TrueFalse, new[] { "True", "False", "Maybe" }, 0)));

    [Fact]
    public void Duplicate_option_texts_are_invalid() =>
        Assert.NotEmpty(Run(Question(QuestionType.SingleChoice, new[] { "Same", "same", "Other" }, 0)));

    [Fact]
    public void A_question_with_a_single_option_is_invalid() =>
        Assert.NotEmpty(Run(Question(QuestionType.SingleChoice, new[] { "Only" }, 0)));

    [Fact]
    public void A_question_with_nine_options_is_invalid() =>
        Assert.NotEmpty(Run(Question(QuestionType.SingleChoice, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" }, 0)));

    private static List<ValidationResult> Run(SubmitAssignmentRequest r) => r.Validate(new ValidationContext(r)).ToList();

    [Fact]
    public void A_submission_with_neither_text_nor_link_is_invalid() =>
        Assert.NotEmpty(Run(new SubmitAssignmentRequest { TextContent = "   " }));

    [Fact]
    public void An_ftp_link_is_invalid() =>
        Assert.NotEmpty(Run(new SubmitAssignmentRequest { LinkUrl = "ftp://example.com/file" }));

    [Fact]
    public void A_javascript_link_is_invalid() =>
        Assert.NotEmpty(Run(new SubmitAssignmentRequest { LinkUrl = "javascript:alert(1)" }));

    [Fact]
    public void An_https_link_alone_is_valid() =>
        Assert.Empty(Run(new SubmitAssignmentRequest { LinkUrl = "https://example.com/work" }));

    [Fact]
    public void A_role_name_is_accepted_in_any_case() =>
        Assert.Empty(new ChangeRoleRequest { Role = "instructor" }.Validate(new ValidationContext(new ChangeRoleRequest())));

    [Fact]
    public void An_unknown_role_is_rejected() =>
        Assert.Single(new ChangeRoleRequest { Role = "Superuser" }.Validate(new ValidationContext(new ChangeRoleRequest())));
}