using Lms.Api.Dtos.Content;

namespace Lms.Api.Services;

public enum ContentActionError
{
    None,
    NotFound,
    LastLessonOfPublishedCourse,
    HasStudentProgress
}

public record ModuleResult(ModuleDto? Module, ContentActionError Error);

public record ModuleListResult(List<ModuleDto>? Modules, ContentActionError Error);

public record LessonResult(LessonDto? Lesson, ContentActionError Error);

public record LessonListResult(List<LessonDto>? Lessons, ContentActionError Error);

public interface IInstructorContentService
{
    Task<ModuleResult> CreateModuleAsync(int courseId, int instructorId, CreateModuleRequest request);
    Task<ModuleResult> UpdateModuleAsync(int moduleId, int instructorId, UpdateModuleRequest request);
    Task<ContentActionError> DeleteModuleAsync(int moduleId, int instructorId);
    Task<ModuleListResult> MoveModuleAsync(int moduleId, int instructorId, MoveDirection direction);

    Task<LessonResult> CreateLessonAsync(int moduleId, int instructorId, CreateLessonRequest request);
    Task<LessonResult> UpdateLessonAsync(int lessonId, int instructorId, UpdateLessonRequest request);
    Task<ContentActionError> DeleteLessonAsync(int lessonId, int instructorId);
    Task<LessonListResult> MoveLessonAsync(int lessonId, int instructorId, MoveDirection direction);
}