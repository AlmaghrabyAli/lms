using Lms.Api.Entities;

namespace Lms.Api.Dtos.Admin;

public record AdminUserDto(
    int Id, string FullName, string Email, string Role, bool IsActive, DateTime CreatedAt,
    int CoursesCount, int EnrollmentsCount);

public record AdminCourseDto(
    int Id, string Title, string Slug, int InstructorId, string InstructorName, string Category, CourseLevel Level,
    bool IsPublished, int EnrollmentCount, int LessonCount, DateTime CreatedAt, DateTime UpdatedAt);

public record TopCourseDto(int CourseId, string Title, int EnrollmentCount);

public record AdminStatsDto(
    int TotalUsers, int Students, int Instructors, int Admins, int InactiveUsers,
    int TotalCourses, int PublishedCourses, int DraftCourses,
    int TotalEnrollments, int TotalLessons, int TotalQuizzes, int TotalAssignments,
    int QuizAttemptsSubmitted, int TotalSubmissions, int GradedSubmissions,
    int NewUsersLast7Days, int EnrollmentsLast7Days, List<TopCourseDto> TopCourses);