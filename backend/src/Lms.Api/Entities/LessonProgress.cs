namespace Lms.Api.Entities;

public class LessonProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}