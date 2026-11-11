using System.ComponentModel.DataAnnotations;

namespace Lms.Api.Dtos.Auth;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Password { get; set; } = string.Empty;
}