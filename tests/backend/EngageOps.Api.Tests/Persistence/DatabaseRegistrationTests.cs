using EngageOps.Api.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Persistence;

public class DatabaseRegistrationTests
{
    [Fact]
    public void ApplicationRegistersNpgsqlDbContext()
    {
        using var factory = new EngageOpsApiFactory();
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}
