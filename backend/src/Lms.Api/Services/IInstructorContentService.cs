using Lms.Api.Dtos.Content;

namespace Lms.Api.Services;

public enum ContentActionError
{
    None,
    NotFound,
    LastLessonOfPublishedCourse
}

public record ModuleResult(ModuleDto? Module, ContentActionError Error);

public record ModuleListResult(List<ModuleDto>? Modules, ContentActionError Error);

public interface IInstructorContentService
{
    Task<ModuleResult> CreateModuleAsync(int courseId, int instructorId, CreateModuleRequest request);
    Task<ModuleResult> UpdateModuleAsync(int moduleId, int instructorId, UpdateModuleRequest request);
    Task<ContentActionError> DeleteModuleAsync(int moduleId, int instructorId);
    Task<ModuleListResult> MoveModuleAsync(int moduleId, int instructorId, MoveDirection direction);
}