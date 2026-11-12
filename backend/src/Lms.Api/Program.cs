using System.Text;
using Lms.Api.Authorization;
using Lms.Api.Data;
using Lms.Api.Data.Seed;
using Lms.Api.Documentation;
using Lms.Api.Entities;
using Lms.Api.Middleware;
using Lms.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Services (Dependency Injection container)
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Errors: one JSON shape (ProblemDetails) for everything, with a traceId to find the log entry
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// CORS: allowed origins come from config (Cors:AllowedOrigins). Empty list = no cross-origin access.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// JWT settings (Key comes from user-secrets in dev, env var Jwt__Key in production)
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random value>\"");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names as issued: sub, email, name, role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.StudentOnly, p => p.RequireRole(Roles.Student));
    options.AddPolicy(Policies.InstructorOnly, p => p.RequireRole(Roles.Instructor));
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInstructorCourseService, InstructorCourseService>();

var app = builder.Build();

// Apply migrations and seed initial data
await DbSeeder.SeedAsync(app.Services);

// Middleware pipeline (order matters)
app.UseExceptionHandler(); // unhandled exceptions -> ProblemDetails (keep this first)
app.UseStatusCodePages();  // empty 401/403/404 responses -> ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Lms API v1");
        options.RoutePrefix = "swagger"; // UI at /swagger
    });
}

app.UseHttpsRedirection();
app.UseCors("Frontend");  // before authentication so preflight (OPTIONS) requests succeed
app.UseAuthentication();  // who are you?  (must come before UseAuthorization)
app.UseAuthorization();   // are you allowed?

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();