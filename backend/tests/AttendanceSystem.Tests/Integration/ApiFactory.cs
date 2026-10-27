using AttendanceSystem.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace AttendanceSystem.Tests.Integration;

/// <summary>Boots the real API in-process with an in-memory DB, in-memory QR store and a controllable clock.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);   // starts at real 'now' so JWT lifetimes stay valid
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "integration-test-key-integration-test-key-0123456789",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Database:Provider"] = "InMemory",
            ["Database:InMemoryName"] = _dbName,
            ["Redis:ConnectionString"] = "",
            ["Seed:Enabled"] = "false"
        }));
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IHostedService>();              // no background rotation/alerts during tests
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }
}
