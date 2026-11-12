namespace Lms.Api.Entities;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;
    public string Category { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int InstructorId { get; set; }
    public User Instructor { get; set; } = null!;
    public ICollection<Module> Modules { get; set; } = new List<Module>();
}