using System.Text;

namespace Lms.Api.Services;

public static class SlugHelper
{
    public const int MaxBaseLength = 200;

    // "Intro to C#!" -> "intro-to-c". Only a-z and 0-9 survive; everything else becomes one dash.
    // If nothing survives (e.g. an all-Arabic title) the base slug is "course".
    public static string Slugify(string text)
    {
        var sb = new StringBuilder();
        var lastWasDash = true; // true at the start so the slug never begins with a dash
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                sb.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > MaxBaseLength) slug = slug[..MaxBaseLength].Trim('-');
        return slug.Length == 0 ? "course" : slug;
    }
}