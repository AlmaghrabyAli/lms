using Lms.Api.Services;
using Xunit;

namespace Lms.Tests.Services;

public class QuizGraderTests
{
    [Fact]
    public void Selecting_the_single_correct_option_is_correct() =>
        Assert.True(QuizGrader.IsQuestionCorrect(new[] { 1 }, new[] { 1 }));

    [Fact]
    public void Selecting_a_wrong_option_is_wrong() =>
        Assert.False(QuizGrader.IsQuestionCorrect(new[] { 1 }, new[] { 2 }));

    [Fact]
    public void Multiple_choice_needs_exactly_the_correct_set_in_any_order() =>
        Assert.True(QuizGrader.IsQuestionCorrect(new[] { 1, 2 }, new[] { 2, 1 }));

    [Fact]
    public void Multiple_choice_with_a_missing_correct_option_is_wrong() =>
        Assert.False(QuizGrader.IsQuestionCorrect(new[] { 1, 2 }, new[] { 1 }));

    [Fact]
    public void Multiple_choice_with_an_extra_wrong_option_is_wrong() =>
        Assert.False(QuizGrader.IsQuestionCorrect(new[] { 1, 2 }, new[] { 1, 2, 3 }));

    [Fact]
    public void Selecting_nothing_is_wrong() =>
        Assert.False(QuizGrader.IsQuestionCorrect(new[] { 1 }, Array.Empty<int>()));

    [Fact]
    public void A_question_without_any_correct_option_is_never_correct() =>
        Assert.False(QuizGrader.IsQuestionCorrect(Array.Empty<int>(), Array.Empty<int>()));

    [Theory]
    [InlineData(2, 3, 67)]
    [InlineData(1, 3, 33)]
    [InlineData(1, 2, 50)]
    [InlineData(1, 8, 13)]
    [InlineData(0, 4, 0)]
    [InlineData(4, 4, 100)]
    [InlineData(0, 0, 0)]
    [InlineData(5, 0, 0)]
    public void Percent_is_rounded_to_the_nearest_whole_number(int earned, int total, int expected) =>
        Assert.Equal(expected, QuizGrader.Percent(earned, total));

    [Theory]
    [InlineData(59, 60, false)]
    [InlineData(60, 60, true)]
    [InlineData(100, 100, true)]
    [InlineData(0, 1, false)]
    public void Passing_means_reaching_the_passing_score(int score, int passing, bool expected) =>
        Assert.Equal(expected, QuizGrader.IsPassed(score, passing));
}