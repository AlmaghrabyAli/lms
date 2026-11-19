using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
            e.Property(u => u.Role).IsRequired().HasMaxLength(20);
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.Property(c => c.Title).IsRequired().HasMaxLength(200);
            e.Property(c => c.Slug).IsRequired().HasMaxLength(220);
            e.Property(c => c.Description).IsRequired().HasMaxLength(4000);
            e.Property(c => c.ThumbnailUrl).HasMaxLength(500);
            e.Property(c => c.Category).IsRequired().HasMaxLength(100);
            e.Property(c => c.Level).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(c => c.Slug).IsUnique();

            // Restrict: deleting a user must NOT silently delete their courses (and students' progress).
            e.HasOne(c => c.Instructor)
             .WithMany(u => u.CoursesTaught)
             .HasForeignKey(c => c.InstructorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Module>(e =>
        {
            e.Property(m => m.Title).IsRequired().HasMaxLength(200);
            e.HasOne(m => m.Course).WithMany(c => c.Modules)
             .HasForeignKey(m => m.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.CourseId, m.OrderIndex });   // not unique: reorder swaps would clash
        });

        modelBuilder.Entity<Lesson>(e =>
        {
            e.Property(l => l.Title).IsRequired().HasMaxLength(200);
            e.Property(l => l.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.VideoUrl).HasMaxLength(500);
            e.Property(l => l.ExternalUrl).HasMaxLength(500);
            e.HasOne(l => l.Module).WithMany(m => m.Lessons)
             .HasForeignKey(l => l.ModuleId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.ModuleId, l.OrderIndex });
        });

        // Phase 3. All four foreign keys are Restrict (NO ACTION in SQL Server):
        // 1) SQL Server refuses "multiple cascade paths" to the same table
        //    (User -> Enrollments and Course -> Modules -> Lessons -> LessonProgresses would both cascade).
        // 2) Students' enrollments and progress must never disappear silently when something else is deleted.
        // The services return a clear 409 before the database ever has to refuse.
        modelBuilder.Entity<Enrollment>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();   // one enrollment per student per course
        });

        modelBuilder.Entity<LessonProgress>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();   // a lesson is completed once per student
        });

        // Phase 6 (quizzes).
        // Cascade: Course -> Quiz -> Question -> AnswerOption (deleting a draft course removes its quiz content).
        // Restrict: everything that belongs to a student's attempt, so results are never wiped silently, and
        //   Quiz -> Module (a module delete must not take the quiz with it; the content service moves the quiz
        //   to course level first). AttemptAnswer -> Question/AnswerOption are Restrict because the attempt path
        //   (Quiz -> Attempt -> Answer) and the question path (Quiz -> Question -> Answer) would be two cascade paths.
        modelBuilder.Entity<Quiz>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CourseId);
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.Property(x => x.Text).IsRequired().HasMaxLength(1000);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Quiz).WithMany(q => q.Questions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.QuizId, x.OrderIndex });
        });

        modelBuilder.Entity<AnswerOption>(e =>
        {
            e.Property(x => x.Text).IsRequired().HasMaxLength(500);
            e.HasOne(x => x.Question).WithMany(q => q.Options).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizAttempt>(e =>
        {
            e.HasOne(x => x.Quiz).WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            // At most ONE open (not yet submitted) attempt per student and quiz, enforced by the database.
            e.HasIndex(x => new { x.UserId, x.QuizId }).IsUnique().HasFilter("[SubmittedAt] IS NULL");
        });

        modelBuilder.Entity<AttemptAnswer>(e =>
        {
            e.HasOne(x => x.Attempt).WithMany(a => a.Answers).HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SelectedOption).WithMany().HasForeignKey(x => x.SelectedOptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AttemptId, x.SelectedOptionId }).IsUnique();   // an option is selected once per attempt
        });
    }
}