using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databaseConnectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("The 'Database' connection string is required.");

builder.Services.AddDbContext<EngageOpsDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AuthenticationEndpoints.AntiforgeryHeaderName;
    options.Cookie.Name = "EngageOps.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddProblemDetails();
// Avoid environment-dependent exception details for malformed Minimal API request bodies.
builder.Services.Configure<RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = false);
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<EngageOpsDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "EngageOps.Authentication";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication is required.")
            .ExecuteAsync(context.HttpContext);
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Access is denied.")
            .ExecuteAsync(context.HttpContext);
    };
});
builder.Services.AddScoped<AssignmentCreator>();
builder.Services.AddScoped<ClientCreator>();
builder.Services.AddScoped<OrganisationMembershipChecker>();
builder.Services.AddScoped<OrganisationProvisioner>();
builder.Services.AddScoped<WorkerCreator>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAssignmentEndpoints();
app.MapAuthenticationEndpoints();
app.MapClientEndpoints();
app.MapOrganisationEndpoints();
app.MapWorkerEndpoints();

app.Run();

public partial class Program;
