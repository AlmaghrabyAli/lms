using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Admin;
using Lms.Api.Dtos.Common;
using Lms.Api.Entities;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminController(IAdminService admin) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Users(
        [FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        string? normalizedRole = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            normalizedRole = Roles.All.FirstOrDefault(r => r.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalizedRole is null)
                return Problem(title: "Invalid role", detail: $"Role must be one of: {string.Join(", ", Roles.All)}.", statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(await admin.ListUsersAsync(search, normalizedRole, isActive, page, pageSize));
    }

    [HttpPut("users/{userId:int}/role")]
    public async Task<ActionResult<AdminUserDto>> ChangeRole(int userId, [FromBody] ChangeRoleRequest request)
    {
        var result = await admin.ChangeRoleAsync(UserId, userId, request.Role!);
        if (result.Error != AdminError.None) return MapError(result.Error, result.Detail, "User not found");
        return Ok(result.Value);
    }

    [HttpPut("users/{userId:int}/active")]
    public async Task<ActionResult<AdminUserDto>> SetActive(int userId, [FromBody] SetActiveRequest request)
    {
        var result = await admin.SetActiveAsync(UserId, userId, request.IsActive!.Value);
        if (result.Error != AdminError.None) return MapError(result.Error, result.Detail, "User not found");
        return Ok(result.Value);
    }

    [HttpGet("courses")]
    public async Task<ActionResult<PagedResult<AdminCourseDto>>> Courses(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        bool? published = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Trim().Equals("published", StringComparison.OrdinalIgnoreCase)) published = true;
            else if (status.Trim().Equals("draft", StringComparison.OrdinalIgnoreCase)) published = false;
            else return Problem(title: "Invalid status", detail: "Status must be 'published' or 'draft'.", statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(await admin.ListCoursesAsync(search, published, page, pageSize));
    }

    [HttpPost("courses/{courseId:int}/unpublish")]
    public async Task<ActionResult<AdminCourseDto>> UnpublishCourse(int courseId)
    {
        var result = await admin.UnpublishCourseAsync(courseId);
        if (result.Error != AdminError.None) return MapError(result.Error, result.Detail, "Course not found");
        return Ok(result.Value);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats() => Ok(await admin.GetStatsAsync());

    private ObjectResult MapError(AdminError error, string? detail, string notFoundTitle) => error switch
    {
        AdminError.NotFound => Problem(title: notFoundTitle, statusCode: StatusCodes.Status404NotFound),
        AdminError.SelfChange => Problem(title: "You cannot change your own account",
            detail: "Ask another admin to change your role or disable your account.", statusCode: StatusCodes.Status409Conflict),
        AdminError.OwnsCourses => Problem(title: "User owns courses",
            detail: detail ?? "This instructor still owns courses.", statusCode: StatusCodes.Status409Conflict),
        _ => Problem(title: "Unexpected error", statusCode: StatusCodes.Status500InternalServerError)
    };
}