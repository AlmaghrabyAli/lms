using Lms.Api.Data;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Tests.Support;

// An isolated in-memory database per test. It does NOT enforce foreign keys or unique indexes, so these tests
// cover the rules written in C# (ownership, enrollment, grading, locking). The database rules (cascades, unique
// indexes, the filtered index) are proven by the PowerShell integration checks against real SQL Server.
public static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    public static async Task<User> AddUserAsync(AppDbContext db, string role)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var user = new User { FullName = $"{role} {id}", Email = $"{id}@test.local", PasswordHash = "x", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Course> AddCourseAsync(AppDbContext db, User instructor, bool published = true, int lessons = 2)
    {
        var course = new Course
        {
            Title = "Test course",
            Slug = "c-" + Guid.NewGuid().ToString("N"),
            Description = "A test course description",
            Category = "Test",
            InstructorId = instructor.Id,
            IsPublished = published
        };
        var module = new Module { Title = "Module", OrderIndex = 1 };
        for (var i = 1; i <= lessons; i++)
            module.Lessons.Add(new Lesson { Title = $"Lesson {i}", Type = LessonType.Text, Content = "text", OrderIndex = i });
        course.Modules.Add(module);
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return course;
    }

    public static async Task EnrollAsync(AppDbContext db, User student, Course course)
    {
        db.Enrollments.Add(new Enrollment { UserId = student.Id, CourseId = course.Id, EnrolledAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    public static async Task CompleteAsync(AppDbContext db, User student, Lesson lesson)
    {
        db.LessonProgresses.Add(new LessonProgress { UserId = student.Id, LessonId = lesson.Id, CompletedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    // A quiz with 2 questions, 4 points: "Single" (1 point, Right/Wrong) and "Multiple" (3 points, A and B are correct, C is wrong).
    public static async Task<Quiz> AddQuizAsync(AppDbContext db, Course course, bool published = true, int passing = 60, int? maxAttempts = null)
    {
        var quiz = new Quiz { CourseId = course.Id, Title = "Quiz", PassingScorePercent = passing, MaxAttempts = maxAttempts, IsPublished = published };

        var single = new Question { Text = "Single", Type = QuestionType.SingleChoice, Points = 1, OrderIndex = 1 };
        single.Options.Add(new AnswerOption { Text = "Right", IsCorrect = true });
        single.Options.Add(new AnswerOption { Text = "Wrong", IsCorrect = false });

        var multiple = new Question { Text = "Multiple", Type = QuestionType.MultipleChoice, Points = 3, OrderIndex = 2 };
        multiple.Options.Add(new AnswerOption { Text = "A", IsCorrect = true });
        multiple.Options.Add(new AnswerOption { Text = "B", IsCorrect = true });
        multiple.Options.Add(new AnswerOption { Text = "C", IsCorrect = false });

        quiz.Questions.Add(single);
        quiz.Questions.Add(multiple);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();
        return quiz;
    }

    public static async Task<Assignment> AddAssignmentAsync(AppDbContext db, Course course, DateTime? due = null, bool published = true)
    {
        var assignment = new Assignment
        {
            CourseId = course.Id,
            Title = "Assignment",
            Instructions = "Do the work",
            DueDate = due,
            MaxScore = 100,
            IsPublished = published
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }
}