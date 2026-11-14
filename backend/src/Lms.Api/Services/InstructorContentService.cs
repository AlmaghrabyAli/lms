using System.Linq.Expressions;
using Lms.Api.Data;
using Lms.Api.Dtos.Content;
using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Services;

public class InstructorContentService(AppDbContext db) : IInstructorContentService
{
    // Projections reused by several methods, so the SQL only selects what the DTO needs.
    private static readonly Expression<Func<Lesson, LessonDto>> ToLessonDto = l => new LessonDto(
        l.Id, l.Title, l.Type, l.VideoUrl, l.Content, l.ExternalUrl, l.DurationMinutes, l.OrderIndex);

    private static readonly Expression<Func<Module, ModuleDto>> ToModuleDto = m => new ModuleDto(
        m.Id, m.Title, m.OrderIndex,
        m.Lessons.OrderBy(l => l.OrderIndex).Select(l => new LessonDto(
            l.Id, l.Title, l.Type, l.VideoUrl, l.Content, l.ExternalUrl, l.DurationMinutes, l.OrderIndex
        )).ToList());

    // ======================= Modules =======================

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

        return new ModuleResult(await GetModuleDtoAsync(module.Id), ContentActionError.None);
    }

    public async Task<ModuleResult> UpdateModuleAsync(int moduleId, int instructorId, UpdateModuleRequest request)
    {
        var module = await GetOwnedModuleAsync(moduleId, instructorId);
        if (module is null) return new ModuleResult(null, ContentActionError.NotFound);

        module.Title = request.Title.Trim();
        module.Course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new ModuleResult(await GetModuleDtoAsync(moduleId), ContentActionError.None);
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

    // ======================= Lessons =======================

    public async Task<LessonResult> CreateLessonAsync(int moduleId, int instructorId, CreateLessonRequest request)
    {
        var module = await GetOwnedModuleAsync(moduleId, instructorId);
        if (module is null) return new LessonResult(null, ContentActionError.NotFound);

        var last = await db.Lessons.Where(l => l.ModuleId == moduleId).MaxAsync(l => (int?)l.OrderIndex);
        var lesson = new Lesson
        {
            ModuleId = moduleId, // from the URL, never from the body
            OrderIndex = (last ?? 0) + 1
        };
        ApplyRequest(lesson, request);

        db.Lessons.Add(lesson);
        module.Course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new LessonResult(await GetLessonDtoAsync(lesson.Id), ContentActionError.None);
    }

    public async Task<LessonResult> UpdateLessonAsync(int lessonId, int instructorId, UpdateLessonRequest request)
    {
        var lesson = await GetOwnedLessonAsync(lessonId, instructorId);
        if (lesson is null) return new LessonResult(null, ContentActionError.NotFound);

        ApplyRequest(lesson, request); // OrderIndex and ModuleId are NOT touched here
        lesson.Module.Course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new LessonResult(await GetLessonDtoAsync(lessonId), ContentActionError.None);
    }

    public async Task<ContentActionError> DeleteLessonAsync(int lessonId, int instructorId)
    {
        var lesson = await GetOwnedLessonAsync(lessonId, instructorId);
        if (lesson is null) return ContentActionError.NotFound;

        var course = lesson.Module.Course;

        // A published course must keep at least one lesson.
        if (course.IsPublished)
        {
            var otherLessonExists = await db.Lessons.AnyAsync(l => l.Module.CourseId == course.Id && l.Id != lessonId);
            if (!otherLessonExists) return ContentActionError.LastLessonOfPublishedCourse;
        }

        var siblings = await db.Lessons
            .Where(l => l.ModuleId == lesson.ModuleId && l.Id != lessonId)
            .OrderBy(l => l.OrderIndex).ThenBy(l => l.Id)
            .ToListAsync();

        db.Lessons.Remove(lesson);
        for (var i = 0; i < siblings.Count; i++) siblings[i].OrderIndex = i + 1;

        course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ContentActionError.None;
    }

    public async Task<LessonListResult> MoveLessonAsync(int lessonId, int instructorId, MoveDirection direction)
    {
        var lesson = await GetOwnedLessonAsync(lessonId, instructorId);
        if (lesson is null) return new LessonListResult(null, ContentActionError.NotFound);

        // Lessons move only inside their own module.
        var siblings = await db.Lessons
            .Where(l => l.ModuleId == lesson.ModuleId)
            .OrderBy(l => l.OrderIndex).ThenBy(l => l.Id)
            .ToListAsync();

        var index = siblings.FindIndex(l => l.Id == lessonId);
        var neighbourIndex = direction == MoveDirection.Up ? index - 1 : index + 1;

        if (neighbourIndex >= 0 && neighbourIndex < siblings.Count)
        {
            var neighbour = siblings[neighbourIndex];
            (lesson.OrderIndex, neighbour.OrderIndex) = (neighbour.OrderIndex, lesson.OrderIndex);
            lesson.Module.Course.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var lessons = await db.Lessons.AsNoTracking()
            .Where(l => l.ModuleId == lesson.ModuleId)
            .OrderBy(l => l.OrderIndex).ThenBy(l => l.Id)
            .Select(ToLessonDto)
            .ToListAsync();

        return new LessonListResult(lessons, ContentActionError.None);
    }

    // ======================= Helpers =======================

    // Copies the request into the entity and nulls every field that does not belong to the chosen type,
    // so changing the type never leaves stale data behind.
    private static void ApplyRequest(Lesson lesson, CreateLessonRequest request)
    {
        lesson.Title = request.Title.Trim();
        lesson.Type = request.Type!.Value;
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.VideoUrl = null;
        lesson.Content = null;
        lesson.ExternalUrl = null;

        switch (lesson.Type)
        {
            case LessonType.Video:
                lesson.VideoUrl = YouTubeUrl.TryNormalize(request.VideoUrl, out var embed)
                    ? embed
                    : throw new InvalidOperationException("Video lesson reached the service with an invalid YouTube url.");
                break;
            case LessonType.Text:
                lesson.Content = request.Content!.Trim();
                break;
            case LessonType.Link:
                lesson.ExternalUrl = request.ExternalUrl!.Trim();
                break;
        }
    }

    // Resource-based authorization: module -> course -> instructor, all inside the query.
    // Tracked (no AsNoTracking) because callers modify it; Course is included to touch UpdatedAt.
    private Task<Module?> GetOwnedModuleAsync(int moduleId, int instructorId) =>
        db.Modules.Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.Course.InstructorId == instructorId);

    // lesson -> module -> course -> instructor
    private Task<Lesson?> GetOwnedLessonAsync(int lessonId, int instructorId) =>
        db.Lessons.Include(l => l.Module).ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId && l.Module.Course.InstructorId == instructorId);

    private Task<ModuleDto?> GetModuleDtoAsync(int moduleId) =>
        db.Modules.AsNoTracking().Where(m => m.Id == moduleId).Select(ToModuleDto).FirstOrDefaultAsync();

    private Task<LessonDto?> GetLessonDtoAsync(int lessonId) =>
        db.Lessons.AsNoTracking().Where(l => l.Id == lessonId).Select(ToLessonDto).FirstOrDefaultAsync();
}