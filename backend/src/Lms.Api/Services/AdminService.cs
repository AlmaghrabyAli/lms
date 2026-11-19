using Lms.Api.Data;
using Lms.Api.Dtos.Admin;
using Lms.Api.Dtos.Common;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class AdminService(AppDbContext db) : IAdminService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private sealed record UserRow(int Id, string FullName, string Email, string Role, bool IsActive, DateTime CreatedAt);
    private sealed record CourseRow(int Id, string Title, string Slug, int InstructorId, string InstructorName, string Category, CourseLevel Level, bool IsPublished, DateTime CreatedAt, DateTime UpdatedAt);

    // ======================= Users =======================

    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, string? role, bool? isActive, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
        }
        if (role is not null) query = query.Where(u => u.Role == role);
        if (isActive is not null) query = query.Where(u => u.IsActive == isActive.Value);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserRow(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync();

        return new PagedResult<AdminUserDto>(await ToUserDtosAsync(rows), page, pageSize, total);
    }

    public async Task<AdminResult<AdminUserDto>> ChangeRoleAsync(int callerId, int userId, string role)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new AdminResult<AdminUserDto>(null, AdminError.NotFound);

        // An admin who could demote themselves could leave the platform without any admin.
        if (callerId == userId) return new AdminResult<AdminUserDto>(null, AdminError.SelfChange);

        var normalized = Roles.All.First(r => r.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user.Role != normalized)
        {
            if (user.Role == Roles.Instructor && await db.Courses.AnyAsync(c => c.InstructorId == userId))
                return new AdminResult<AdminUserDto>(null, AdminError.OwnsCourses,
                    "This instructor still owns courses. Courses cannot be left without an instructor, so the role cannot change.");

            user.Role = normalized;
            await db.SaveChangesAsync();
        }

        return new AdminResult<AdminUserDto>(await BuildUserDtoAsync(user), AdminError.None);
    }

    public async Task<AdminResult<AdminUserDto>> SetActiveAsync(int callerId, int userId, bool isActive)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new AdminResult<AdminUserDto>(null, AdminError.NotFound);
        if (callerId == userId) return new AdminResult<AdminUserDto>(null, AdminError.SelfChange);

        if (user.IsActive != isActive)
        {
            user.IsActive = isActive;
            await db.SaveChangesAsync();
        }

        return new AdminResult<AdminUserDto>(await BuildUserDtoAsync(user), AdminError.None);
    }

    // ======================= Courses =======================

    public async Task<PagedResult<AdminCourseDto>> ListCoursesAsync(string? search, bool? published, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Courses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Title.Contains(term) || c.Instructor.FullName.Contains(term));
        }
        if (published is not null) query = query.Where(c => c.IsPublished == published.Value);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CourseRow(c.Id, c.Title, c.Slug, c.InstructorId, c.Instructor.FullName, c.Category, c.Level, c.IsPublished, c.CreatedAt, c.UpdatedAt))
            .ToListAsync();

        return new PagedResult<AdminCourseDto>(await ToCourseDtosAsync(rows), page, pageSize, total);
    }

    public async Task<AdminResult<AdminCourseDto>> UnpublishCourseAsync(int courseId)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null) return new AdminResult<AdminCourseDto>(null, AdminError.NotFound);

        // Unpublishing only hides the course: enrollments and progress stay, nothing is deleted.
        if (course.IsPublished)
        {
            course.IsPublished = false;
            course.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var row = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new CourseRow(c.Id, c.Title, c.Slug, c.InstructorId, c.Instructor.FullName, c.Category, c.Level, c.IsPublished, c.CreatedAt, c.UpdatedAt))
            .FirstAsync();
        var dtos = await ToCourseDtosAsync(new List<CourseRow> { row });
        return new AdminResult<AdminCourseDto>(dtos[0], AdminError.None);
    }

    // ======================= Stats =======================

    public async Task<AdminStatsDto> GetStatsAsync()
    {
        // Plain count queries, one after the other (a DbContext is not thread-safe). Only admins call this.
        var since = DateTime.UtcNow.AddDays(-7);

        var totalUsers = await db.Users.CountAsync();
        var students = await db.Users.CountAsync(u => u.Role == Roles.Student);
        var instructors = await db.Users.CountAsync(u => u.Role == Roles.Instructor);
        var admins = await db.Users.CountAsync(u => u.Role == Roles.Admin);
        var inactiveUsers = await db.Users.CountAsync(u => !u.IsActive);

        var totalCourses = await db.Courses.CountAsync();
        var publishedCourses = await db.Courses.CountAsync(c => c.IsPublished);

        var totalEnrollments = await db.Enrollments.CountAsync();
        var totalLessons = await db.Lessons.CountAsync();
        var totalQuizzes = await db.Quizzes.CountAsync();
        var totalAssignments = await db.Assignments.CountAsync();
        var attemptsSubmitted = await db.QuizAttempts.CountAsync(a => a.SubmittedAt != null);
        var totalSubmissions = await db.Submissions.CountAsync();
        var gradedSubmissions = await db.Submissions.CountAsync(s => s.GradedAt != null);

        var newUsers = await db.Users.CountAsync(u => u.CreatedAt >= since);
        var newEnrollments = await db.Enrollments.CountAsync(e => e.EnrolledAt >= since);

        var topRows = await db.Enrollments.AsNoTracking()
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.CourseId)
            .Take(5)
            .ToListAsync();
        var topIds = topRows.Select(x => x.CourseId).ToList();
        var titles = await db.Courses.AsNoTracking()
            .Where(c => topIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title })
            .ToDictionaryAsync(c => c.Id, c => c.Title);
        var top = topRows.Select(x => new TopCourseDto(x.CourseId, titles.GetValueOrDefault(x.CourseId) ?? string.Empty, x.Count)).ToList();

        return new AdminStatsDto(
            totalUsers, students, instructors, admins, inactiveUsers,
            totalCourses, publishedCourses, totalCourses - publishedCourses,
            totalEnrollments, totalLessons, totalQuizzes, totalAssignments,
            attemptsSubmitted, totalSubmissions, gradedSubmissions,
            newUsers, newEnrollments, top);
    }

    // ======================= Helpers =======================

    // Course and enrollment counts for the users on this page only: two grouped queries, no loop.
    private async Task<List<AdminUserDto>> ToUserDtosAsync(List<UserRow> rows)
    {
        var ids = rows.Select(r => r.Id).ToList();

        var courseCounts = await db.Courses.AsNoTracking()
            .Where(c => ids.Contains(c.InstructorId))
            .GroupBy(c => c.InstructorId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var enrollmentCounts = await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.UserId))
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return rows.Select(r => new AdminUserDto(
            r.Id, r.FullName, r.Email, r.Role, r.IsActive, r.CreatedAt,
            courseCounts.GetValueOrDefault(r.Id), enrollmentCounts.GetValueOrDefault(r.Id))).ToList();
    }

    private async Task<AdminUserDto> BuildUserDtoAsync(User user)
    {
        var dtos = await ToUserDtosAsync(new List<UserRow>
        {
            new(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.CreatedAt)
        });
        return dtos[0];
    }

    private async Task<List<AdminCourseDto>> ToCourseDtosAsync(List<CourseRow> rows)
    {
        var ids = rows.Select(r => r.Id).ToList();

        var enrollmentCounts = await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.CourseId))
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count);

        var lessonCounts = await db.Lessons.AsNoTracking()
            .Where(l => ids.Contains(l.Module.CourseId))
            .GroupBy(l => l.Module.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count);

        return rows.Select(r => new AdminCourseDto(
            r.Id, r.Title, r.Slug, r.InstructorId, r.InstructorName, r.Category, r.Level, r.IsPublished,
            enrollmentCounts.GetValueOrDefault(r.Id), lessonCounts.GetValueOrDefault(r.Id), r.CreatedAt, r.UpdatedAt)).ToList();
    }
}