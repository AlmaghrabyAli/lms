namespace Lms.Api.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Student;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: courses this user teaches (only meaningful for Instructors)
    public ICollection<Course> CoursesTaught { get; set; } = new List<Course>();
}