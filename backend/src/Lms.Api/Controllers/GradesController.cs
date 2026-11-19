using System.Security.Claims;
using Lms.Api.Authorization;
using Lms.Api.Dtos.Learning;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Api.Controllers;

[ApiController]
[Route("api/me/grades")]
[Authorize(Policy = Policies.StudentOnly)]
public class GradesController(IGradesService grades) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<GradesDto>> Get() => Ok(await grades.GetAsync(UserId));
}