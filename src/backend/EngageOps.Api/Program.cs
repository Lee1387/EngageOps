using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databaseConnectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("The 'Database' connection string is required.");

builder.Services.AddDbContext<EngageOpsDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<EngageOpsDbContext>();
builder.Services.AddScoped<OrganisationProvisioner>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
