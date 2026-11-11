using Lms.Api.Data;
using Lms.Api.Dtos.Auth;
using Lms.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public enum AuthError { None, EmailTaken, InvalidCredentials, AccountDisabled }

public record AuthResult(AuthResponse? Response, AuthError Error)
{
    public bool Succeeded => Error == AuthError.None;
}

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<UserDto?> GetCurrentUserAsync(int userId);
}

public class AuthService(AppDbContext db, ITokenService tokens) : IAuthService
{
    // Used when the email does not exist, so response time looks the same as a wrong password.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password");

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var email = Normalize(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return Fail(AuthError.EmailTaken);
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.RegisterAsInstructor ? Roles.Instructor : Roles.Student
        };

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Two requests with the same email at the same moment: the unique index rejected the second one.
            return Fail(AuthError.EmailTaken);
        }

        return Ok(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !passwordValid)
        {
            return Fail(AuthError.InvalidCredentials);
        }

        // Checked only after the password is correct, so wrong guesses learn nothing about the account.
        if (!user.IsActive)
        {
            return Fail(AuthError.AccountDisabled);
        }

        return Ok(user);
    }

    public Task<UserDto?> GetCurrentUserAsync(int userId) =>
        db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role))
            .FirstOrDefaultAsync();

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static AuthResult Fail(AuthError error) => new(null, error);

    private AuthResult Ok(User user)
    {
        var token = tokens.CreateToken(user);
        var response = new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new UserDto(user.Id, user.FullName, user.Email, user.Role));
        return new AuthResult(response, AuthError.None);
    }
}