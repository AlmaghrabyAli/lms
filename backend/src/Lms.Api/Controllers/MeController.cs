using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Learning;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Policy = Policies.StudentOnly)]
public class MeController(IDashboardService dashboard) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard()
        => Ok(await dashboard.GetAsync(UserId));
}