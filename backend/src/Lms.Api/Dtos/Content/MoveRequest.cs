using System.ComponentModel.DataAnnotations;

namespace Lms.Api.Dtos.Content;

public enum MoveDirection
{
    Up,
    Down
}

public class MoveRequest
{
    // Nullable + [Required]: a missing "direction" becomes 400 instead of silently meaning "Up".
    [Required]
    public MoveDirection? Direction { get; set; }
}