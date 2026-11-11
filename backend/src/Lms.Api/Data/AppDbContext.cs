using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();

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

            // One instructor -> many courses.
            // Restrict: deleting a user must NOT silently delete their courses
            // (and with them students' enrollments and progress). We deactivate users instead.
            e.HasOne(c => c.Instructor)
             .WithMany(u => u.CoursesTaught)
             .HasForeignKey(c => c.InstructorId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}