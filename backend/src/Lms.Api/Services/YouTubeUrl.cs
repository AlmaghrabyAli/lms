using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Lms.Api.Services;

public static class YouTubeUrl
{
    // The regex is used ONLY on the 11-character video id, never on the whole URL.
    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

    public static bool TryNormalize(string? input, out string embedUrl)
    {
        embedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        var host = uri.Host.ToLowerInvariant(); // exact host match: "youtube.com.evil.com" is NOT accepted
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? id = null;

        if (host == "youtu.be")
        {
            if (segments.Length == 1) id = segments[0];
        }
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            if (segments.Length == 1 && segments[0] == "watch")
            {
                var query = QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("v", out var v)) id = v.ToString();
            }
            else if (segments.Length == 2 && (segments[0] == "embed" || segments[0] == "shorts"))
            {
                id = segments[1];
            }
        }

        if (id is null || !IdPattern.IsMatch(id)) return false;

        embedUrl = $"https://www.youtube.com/embed/{id}"; // rebuilt from scratch, nothing from the input is reused
        return true;
    }
}