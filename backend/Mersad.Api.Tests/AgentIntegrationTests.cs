using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mersad.Api.Configuration;
using Mersad.Api.Data;
using Mersad.Api.Models;

namespace Mersad.Api.Tests;

public sealed class AgentIntegrationTests : IDisposable
{
    private readonly AgentApiFactory _factory = new();
    private readonly HttpClient _client;

    public AgentIntegrationTests()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<MersadDbContext>()
            .Database.EnsureCreated();
    }

    [Fact]
    public async Task AgentRegistersAndSendsTelemetry_EventsAndAlertsAreStored()
    {
        var agentId = Guid.NewGuid();

        var registration = await _client.PostAsJsonAsync(
            "/api/devices",
            new
            {
                agentId,
                hostname = "TEST-ENDPOINT",
                os = "Windows",
                osVersion = "11"
            });

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registeredDevice = await registration.Content
            .ReadFromJsonAsync<Device>();
        Assert.NotNull(registeredDevice);
        Assert.Equal(agentId, registeredDevice.AgentId);

        // These payloads match the separate process and network batches sent
        // by TelemetryClient in the native agent.
        var processResponse = await _client.PostAsJsonAsync(
            "/api/telemetry",
            new
            {
                agentId,
                events = new[]
                {
                    new
                    {
                        type = "process",
                        timestamp = DateTimeOffset.UtcNow,
                        data = new
                        {
                            processId = 4242,
                            processName = "powershell.exe"
                        }
                    }
                }
            });

        var networkResponse = await _client.PostAsJsonAsync(
            "/api/telemetry",
            new
            {
                agentId,
                events = new[]
                {
                    new
                    {
                        type = "network",
                        timestamp = DateTimeOffset.UtcNow,
                        data = new
                        {
                            processId = 4242,
                            localAddress = "192.168.1.10",
                            localPort = 50000,
                            remoteAddress = "203.0.113.10",
                            remotePort = 1337,
                            state = "Established"
                        }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, processResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, networkResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MersadDbContext>();

        var processEvent = await db.ProcessEvents.SingleAsync();
        var networkEvent = await db.NetworkEvents.SingleAsync();
        var alerts = await db.Alerts.ToListAsync();

        Assert.Equal(registeredDevice.Id, processEvent.DeviceId);
        Assert.Equal(4242, processEvent.ProcessId);
        Assert.Equal("powershell.exe", processEvent.ProcessName);
        Assert.Equal(registeredDevice.Id, networkEvent.DeviceId);
        Assert.Equal(1337, networkEvent.RemotePort);
        Assert.Contains(alerts, alert =>
            alert.DetectionRuleId == "PROC-001" &&
            alert.ProcessEventId == processEvent.Id);
        Assert.Contains(alerts, alert =>
            alert.DetectionRuleId == "NET-001" &&
            alert.NetworkEventId == networkEvent.Id);
    }

    [Fact]
    public async Task TelemetryFromUnregisteredAgent_IsRejected()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/telemetry",
            new
            {
                agentId = Guid.NewGuid(),
                events = new[]
                {
                    new
                    {
                        type = "process",
                        timestamp = DateTimeOffset.UtcNow,
                        data = new
                        {
                            processId = 1234,
                            processName = "example.exe"
                        }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegisteringSameAgentTwice_UpdatesExistingDevice()
    {
        var agentId = Guid.NewGuid();
        var initial = new
        {
            agentId,
            hostname = "OLD-NAME",
            os = "Windows",
            osVersion = "10"
        };

        var first = await _client.PostAsJsonAsync("/api/devices", initial);
        var firstDevice = await first.Content.ReadFromJsonAsync<Device>();

        var second = await _client.PostAsJsonAsync(
            "/api/devices",
            new
            {
                agentId,
                hostname = "NEW-NAME",
                os = "Windows",
                osVersion = "11"
            });
        var secondDevice = await second.Content.ReadFromJsonAsync<Device>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(firstDevice);
        Assert.NotNull(secondDevice);
        Assert.Equal(firstDevice.Id, secondDevice.Id);
        Assert.Equal("NEW-NAME", secondDevice.Hostname);

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(
            1,
            await scope.ServiceProvider.GetRequiredService<MersadDbContext>()
                .Devices.CountAsync(device => device.AgentId == agentId));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private sealed class AgentApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public AgentApiFactory()
        {
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<DetectionOptions>(options =>
                {
                    options.SuspiciousPowerShellEnabled = true;
                    options.SuspiciousNetworkPortEnabled = true;
                    options.PowerShellSuspiciousNetworkEnabled = true;
                    options.SuspiciousNetworkPorts = [4444, 1337, 31337];
                });
                services.RemoveAll<DbContextOptions<MersadDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<MersadDbContext>>();
                services.AddDbContext<MersadDbContext>(options =>
                    options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}
