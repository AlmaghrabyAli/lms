using System.ComponentModel.DataAnnotations;

namespace Lms.Api.Dtos.Auth;

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    // Demo-friendly toggle. Admin can never be requested from the client.
    public bool RegisterAsInstructor { get; set; }
}