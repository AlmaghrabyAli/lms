using Lms.Api.Entities;

namespace Lms.Api.Dtos.Courses;

// Public catalog DTOs: no email, no lesson content, no urls of lessons, no IsPublished.
public record CourseSummaryDto(
    int Id, string Title, string Slug, string Description, string? ThumbnailUrl,
    CourseLevel Level, string Category, string InstructorName,
    int ModuleCount, int LessonCount, int TotalDurationMinutes);

public record LessonOutlineDto(int Id, string Title, LessonType Type, int? DurationMinutes, int OrderIndex);

public record PublicModuleDto(int Id, string Title, int OrderIndex, List<LessonOutlineDto> Lessons);

public record CourseDetailDto(
    int Id, string Title, string Slug, string Description, string? ThumbnailUrl,
    CourseLevel Level, string Category, string InstructorName,
    DateTime CreatedAt, DateTime UpdatedAt,
    int ModuleCount, int LessonCount, int TotalDurationMinutes,
    List<PublicModuleDto> Modules);