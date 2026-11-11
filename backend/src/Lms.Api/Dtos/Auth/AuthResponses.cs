namespace Lms.Api.Dtos.Auth;

public record UserDto(int Id, string FullName, string Email, string Role);

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);