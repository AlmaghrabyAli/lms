namespace Lms.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Lms.Api";
    public string Audience { get; set; } = "Lms.Client";
    public int ExpiryMinutes { get; set; } = 60;
}