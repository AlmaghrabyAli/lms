namespace Lms.Api.Entities;

public class Lesson
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public LessonType Type { get; set; } = LessonType.Text;
    public string? VideoUrl { get; set; }      // YouTube embed URL (Type = Video)
    public string? Content { get; set; }       // text / markdown (Type = Text)
    public string? ExternalUrl { get; set; }   // external link (Type = Link)
    public int? DurationMinutes { get; set; }
    public int OrderIndex { get; set; }
    public int ModuleId { get; set; }
    public Module Module { get; set; } = null!;
}