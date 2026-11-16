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
    }
}