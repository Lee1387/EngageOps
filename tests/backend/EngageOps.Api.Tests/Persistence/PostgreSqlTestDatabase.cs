using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EngageOps.Api.Tests.Persistence;

internal static class PostgreSqlTestDatabase
{
    public static PostgreSqlContainer CreateContainer() =>
        new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    public static DbContextOptions<EngageOpsDbContext> CreateContextOptions(
        PostgreSqlContainer container) =>
        new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
}
