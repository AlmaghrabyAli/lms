using System.Linq.Expressions;
using Lms.Api.Data;
using Lms.Api.Dtos.Content;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class InstructorContentService(AppDbContext db) : IInstructorContentService
{
    // One projection reused by create / update / move, so the SQL only selects what the DTO needs.
    private static readonly Expression<Func<Module, ModuleDto>> ToModuleDto = m => new ModuleDto(
        m.Id, m.Title, m.OrderIndex,
        m.Lessons.OrderBy(l => l.OrderIndex).Select(l => new LessonDto(
            l.Id, l.Title, l.Type, l.VideoUrl, l.Content, l.ExternalUrl, l.DurationMinutes, l.OrderIndex
        )).ToList());

    public async Task<ModuleResult> CreateModuleAsync(int courseId, int instructorId, CreateModuleRequest request)
    {
        // Ownership: the course is loaded only if it belongs to this instructor.
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        if (course is null) return new ModuleResult(null, ContentActionError.NotFound);

        var last = await db.Modules.Where(m => m.CourseId == courseId).MaxAsync(m => (int?)m.OrderIndex);
        var module = new Module
        {
            Title = request.Title.Trim(),
            OrderIndex = (last ?? 0) + 1, // first module = 1; CourseId comes from the URL, never from the body
            CourseId = courseId
        };

        db.Modules.Add(module);
        course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new ModuleResult(await GetDtoAsync(module.Id), ContentActionError.None);
    }

    public async Task<ModuleResult> UpdateModuleAsync(int moduleId, int instructorId, UpdateModuleRequest request)
    {
        var module = await GetOwnedModuleAsync(moduleId, instructorId);
        if (module is null) return new ModuleResult(null, ContentActionError.NotFound);

        module.Title = request.Title.Trim();
        module.Course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new ModuleResult(await GetDtoAsync(moduleId), ContentActionError.None);
    }

    public async Task<ContentActionError> DeleteModuleAsync(int moduleId, int instructorId)
    {
        var module = await GetOwnedModuleAsync(moduleId, instructorId);
        if (module is null) return ContentActionError.NotFound;

        var course = module.Course;

        // A published course must always keep at least one lesson (same rule as publishing).
        if (course.IsPublished)
        {
            var lessonRemains = await db.Lessons.AnyAsync(l => l.Module.CourseId == course.Id && l.ModuleId != moduleId);
            if (!lessonRemains) return ContentActionError.LastLessonOfPublishedCourse;
        }

        var siblings = await db.Modules
            .Where(m => m.CourseId == course.Id && m.Id != moduleId)
            .OrderBy(m => m.OrderIndex).ThenBy(m => m.Id)
            .ToListAsync();

        db.Modules.Remove(module); // the database cascade removes the module's lessons

        // Compact the remaining modules to 1..n in the same SaveChanges.
        for (var i = 0; i < siblings.Count; i++) siblings[i].OrderIndex = i + 1;

        course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ContentActionError.None;
    }

    public async Task<ModuleListResult> MoveModuleAsync(int moduleId, int instructorId, MoveDirection direction)
    {
        var module = await GetOwnedModuleAsync(moduleId, instructorId);
        if (module is null) return new ModuleListResult(null, ContentActionError.NotFound);

        var siblings = await db.Modules
            .Where(m => m.CourseId == module.CourseId)
            .OrderBy(m => m.OrderIndex).ThenBy(m => m.Id)
            .ToListAsync();

        var index = siblings.FindIndex(m => m.Id == moduleId);
        var neighbourIndex = direction == MoveDirection.Up ? index - 1 : index + 1;

        // At the edges there is no neighbour: nothing changes, and it is still a normal 200.
        if (neighbourIndex >= 0 && neighbourIndex < siblings.Count)
        {
            var neighbour = siblings[neighbourIndex];
            (module.OrderIndex, neighbour.OrderIndex) = (neighbour.OrderIndex, module.OrderIndex);
            module.Course.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var modules = await db.Modules.AsNoTracking()
            .Where(m => m.CourseId == module.CourseId)
            .OrderBy(m => m.OrderIndex).ThenBy(m => m.Id)
            .Select(ToModuleDto)
            .ToListAsync();

        return new ModuleListResult(modules, ContentActionError.None);
    }

    // Resource-based authorization: module -> course -> instructor, all inside the query.
    // Tracked (no AsNoTracking) because callers modify it; Course is included to touch UpdatedAt.
    private Task<Module?> GetOwnedModuleAsync(int moduleId, int instructorId) =>
        db.Modules.Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.Course.InstructorId == instructorId);

    private Task<ModuleDto?> GetDtoAsync(int moduleId) =>
        db.Modules.AsNoTracking().Where(m => m.Id == moduleId).Select(ToModuleDto).FirstOrDefaultAsync();
}