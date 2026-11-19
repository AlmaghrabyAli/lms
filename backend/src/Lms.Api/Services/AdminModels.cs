namespace Lms.Api.Services;

public enum AdminError
{
    None,
    NotFound,     // 404
    SelfChange,   // 409 an admin cannot change their own role or disable themselves
    OwnsCourses   // 409 an instructor who still owns courses cannot lose the Instructor role
}

public record AdminResult<T>(T? Value, AdminError Error, string? Detail = null) where T : class;