using Lms.Api.Entities;

namespace Lms.Api.Services;

public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult CreateToken(User user);
}