using Lms.Api.Dtos.Content;
using Lms.Api.Entities;

namespace Lms.Api.Dtos.Courses;

public record InstructorCourseDto(
    int Id,
    string Title,
    string Slug,
    string Description,
    string? ThumbnailUrl,
    CourseLevel Level,
    string Category,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ModuleCount,
    int LessonCount);

public record InstructorCourseDetailDto(
    int Id,
    string Title,
    string Slug,
    string Description,
    string? ThumbnailUrl,
    CourseLevel Level,
    string Category,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ModuleDto> Modules);