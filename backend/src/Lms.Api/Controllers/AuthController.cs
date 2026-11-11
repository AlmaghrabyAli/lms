using System.Security.Claims;
using Lms.Api.Dtos.Auth;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await auth.RegisterAsync(request);
        return result.Error switch
        {
            AuthError.None => StatusCode(StatusCodes.Status201Created, result.Response),
            AuthError.EmailTaken => Problem(title: "Email already registered", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest)
        };
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await auth.LoginAsync(request);
        return result.Error switch
        {
            AuthError.None => Ok(result.Response),
            AuthError.AccountDisabled => Problem(title: "This account has been deactivated", statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(title: "Invalid email or password", statusCode: StatusCodes.Status401Unauthorized)
        };
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            return Unauthorized();
        }

        var me = await auth.GetCurrentUserAsync(userId);
        return me is null ? Unauthorized() : Ok(me);
    }
}