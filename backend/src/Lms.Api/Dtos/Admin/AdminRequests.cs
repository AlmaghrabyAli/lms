using System.ComponentModel.DataAnnotations;
using Lms.Api.Entities;

namespace Lms.Api.Dtos.Admin;

public class ChangeRoleRequest : IValidatableObject
{
    [Required]
    public string? Role { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Role) &&
            !Roles.All.Any(r => r.Equals(Role.Trim(), StringComparison.OrdinalIgnoreCase)))
            yield return new ValidationResult($"Role must be one of: {string.Join(", ", Roles.All)}.", new[] { nameof(Role) });
    }
}

public class SetActiveRequest
{
    // Nullable + Required: a missing field must be a 400, never a silent "false" that disables someone.
    [Required]
    public bool? IsActive { get; set; }
}