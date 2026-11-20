using Lms.Api.Services;
using Xunit;

namespace Lms.Tests.Services;

public class ProgressCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(5, 5, 100)]
    [InlineData(1, 2, 50)]
    [InlineData(7, 8, 88)]
    public void Percent_is_completed_over_total_rounded_and_never_divides_by_zero(int completed, int total, int expected) =>
        Assert.Equal(expected, ProgressCalculator.Percent(completed, total));

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, true)]
    [InlineData(4, 5, false)]
    public void A_course_is_complete_only_when_it_has_lessons_and_all_are_done(int completed, int total, bool expected) =>
        Assert.Equal(expected, ProgressCalculator.IsComplete(completed, total));
}