using Lms.Api.Dtos.Common;
using Lms.Api.Dtos.Courses;
using Lms.Api.Entities;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api/courses")]
[AllowAnonymous]
public class CoursesController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseSummaryDto>>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? level,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        CourseLevel? parsedLevel = null;
        if (!string.IsNullOrWhiteSpace(level))
        {
            // Strict: only the names Beginner / Intermediate / Advanced (any case). Numbers like "1" are rejected.
            var match = Enum.GetNames<CourseLevel>()
                .FirstOrDefault(n => n.Equals(level.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return Problem(
                    title: "Invalid level",
                    detail: $"Level must be one of: {string.Join(", ", Enum.GetNames<CourseLevel>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            parsedLevel = Enum.Parse<CourseLevel>(match);
        }

        return Ok(await catalog.ListAsync(search, category, parsedLevel, page, pageSize));
    }

    // A literal segment wins over {slug}, so this is never captured by the route below.
    [HttpGet("categories")]
    public async Task<ActionResult<List<string>>> Categories() => Ok(await catalog.CategoriesAsync());

    [HttpGet("{slug}")]
    public async Task<ActionResult<CourseDetailDto>> Get(string slug)
    {
        var course = await catalog.GetBySlugAsync(slug);
        if (course is null) return Problem(title: "Course not found", statusCode: StatusCodes.Status404NotFound);
        return Ok(course);
    }
}