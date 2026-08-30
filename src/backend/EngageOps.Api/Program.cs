using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databaseConnectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("The 'Database' connection string is required.");

builder.Services.AddDbContext<EngageOpsDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
