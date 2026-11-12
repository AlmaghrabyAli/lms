using Lms.Api.Entities;

namespace Lms.Api.Dtos.Content;

public record LessonDto(
    int Id,
    string Title,
    LessonType Type,
    string? VideoUrl,
    string? Content,
    string? ExternalUrl,
    int? DurationMinutes,
    int OrderIndex);

public record ModuleDto(int Id, string Title, int OrderIndex, List<LessonDto> Lessons);