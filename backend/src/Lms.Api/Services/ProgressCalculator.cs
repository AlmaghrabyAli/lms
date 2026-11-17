namespace Lms.Api.Services;

// One place for the progress rule, so every endpoint (and the Phase 8 unit tests) uses the same maths.
public static class ProgressCalculator
{
    // completed / total as a whole percent; 0 when the course has no lessons (never divides by zero).
    public static int Percent(int completed, int total)
    {
        if (total <= 0) return 0;
        return (int)Math.Round(completed * 100.0 / total, MidpointRounding.AwayFromZero);
    }

    public static bool IsComplete(int completed, int total) => total > 0 && completed >= total;
}