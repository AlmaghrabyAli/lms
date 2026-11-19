using Lms.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Authorization;

// Runs after a token's signature and expiry were accepted. A JWT cannot be revoked, so on every authenticated
// request we ask the database: the account must still exist, be active, and still have the role the token claims.
// A disabled user or a changed role therefore takes effect immediately (401, sign in again).
public static class ActiveUserCheck
{
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var sub = context.Principal?.FindFirst("sub")?.Value;
        var role = context.Principal?.FindFirst("role")?.Value;
        if (!int.TryParse(sub, out var userId))
        {
            context.Fail("Invalid token.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Role })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null || !user.IsActive || user.Role != role)
            context.Fail("The account is disabled or its role changed. Sign in again.");
    }
}