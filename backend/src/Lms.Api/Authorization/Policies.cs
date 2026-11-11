namespace Lms.Api.Authorization;

public static class Policies
{
    public const string StudentOnly = "StudentOnly";
    public const string InstructorOnly = "InstructorOnly";
    public const string AdminOnly = "AdminOnly";
}