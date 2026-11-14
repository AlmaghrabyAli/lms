using System.ComponentModel.DataAnnotations;

namespace Lms.Api.Dtos.Content;

public class CreateModuleRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;
}