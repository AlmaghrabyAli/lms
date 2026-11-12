using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

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
    }
}