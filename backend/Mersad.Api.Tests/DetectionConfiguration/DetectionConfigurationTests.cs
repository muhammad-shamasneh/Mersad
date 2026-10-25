
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Mersad.Api.Configuration;
using Mersad.Api.Detection;
using Mersad.Api.Detection.Rules;
using Mersad.Api.Models;

namespace Mersad.Api.Tests;

public class DetectionConfigurationTests
{
    private static IOptions<DetectionOptions> Options(
        DetectionOptions options) =>
        Microsoft.Extensions.Options.Options.Create(options);


    private static ProcessEvent Process(
        string name,
        int processId = 100,
        Guid? deviceId = null)
    {
        return new ProcessEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId ?? Guid.NewGuid(),
            ProcessId = processId,
            ProcessName = name,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static NetworkEvent Network(
        int remotePort,
        int processId = 100,
        Guid? deviceId = null)
    {
        return new NetworkEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId ?? Guid.NewGuid(),
            ProcessId = processId,
            LocalAddress = "192.168.1.10",
            LocalPort = 50000,
            RemoteAddress = "203.0.113.10",
            RemotePort = remotePort,
            State = "Established",
            Timestamp = DateTimeOffset.UtcNow
        };
    }


    private static DetectionContext Context(
        params ProcessEvent[] processes) =>
        new(processes, Array.Empty<NetworkEvent>());

    private static DetectionContext NetworkContext(
        params NetworkEvent[] networks) =>
        new(Array.Empty<ProcessEvent>(), networks);

    [Fact]
    public void DefaultConfiguration_IsValid()
    {
        var options = new DetectionOptions();

        Assert.True(options.IsValid());
        Assert.True(options.SuspiciousPowerShellEnabled);
        Assert.True(options.SuspiciousNetworkPortEnabled);
        Assert.True(options.PowerShellSuspiciousNetworkEnabled);
        Assert.Empty(options.SuspiciousNetworkPorts);
    }


    [Fact]
    public void ConfigurationBinding_LoadsConfiguredDetectionOptions()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Detection:SuspiciousPowerShellEnabled"] = "true",
            ["Detection:SuspiciousNetworkPortEnabled"] = "true",
            ["Detection:PowerShellSuspiciousNetworkEnabled"] = "false",
            ["Detection:SuspiciousNetworkPorts:0"] = "4444",
            ["Detection:SuspiciousNetworkPorts:1"] = "1337",
            ["Detection:SuspiciousNetworkPorts:2"] = "31337"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var options = new DetectionOptions();

        configuration
            .GetSection(DetectionOptions.SectionName)
            .Bind(options);

        Assert.True(options.IsValid());
        Assert.True(options.SuspiciousPowerShellEnabled);
        Assert.True(options.SuspiciousNetworkPortEnabled);
        Assert.False(options.PowerShellSuspiciousNetworkEnabled);
        Assert.Equal(
            new[] { 4444, 1337, 31337 },
            options.SuspiciousNetworkPorts);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void InvalidPort_IsRejected(int port)
    {
        var options = new DetectionOptions
        {
            SuspiciousNetworkPorts = [port]
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void DuplicatePorts_AreRejected()
    {
        var options = new DetectionOptions
        {
            SuspiciousNetworkPorts = [1337, 1337]
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void NullPortList_IsRejected()
    {
        var options = new DetectionOptions
        {
            SuspiciousNetworkPorts = null!
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void SuspiciousPowerShellRule_DetectsPowerShell()
    {
        var rule = new SuspiciousPowerShellRule(
            Options(new DetectionOptions()));

        var detections = rule.Evaluate(
            Context(Process("powershell.exe"))).ToList();

        var detection = Assert.Single(detections);
        Assert.Equal("PROC-001", detection.RuleId);
    }

    [Fact]
    public void SuspiciousPowerShellRule_IsCaseInsensitive()
    {
        var rule = new SuspiciousPowerShellRule(
            Options(new DetectionOptions()));

        var detections = rule.Evaluate(
            Context(Process("PowerShell.EXE"))).ToList();

        Assert.Single(detections);
    }

    [Fact]
    public void SuspiciousPowerShellRule_WhenDisabled_ProducesNoDetection()
    {
        var rule = new SuspiciousPowerShellRule(
            Options(new DetectionOptions
            {
                SuspiciousPowerShellEnabled = false
            }));

        var detections = rule.Evaluate(
            Context(Process("powershell.exe"))).ToList();

        Assert.Empty(detections);
    }

    [Fact]
    public void SuspiciousNetworkPortRule_DetectsConfiguredPort()
    {
        var rule = new SuspiciousNetworkPortRule(
            Options(new DetectionOptions
            {
                SuspiciousNetworkPorts = [8080]
            }));

        var detections = rule.Evaluate(
            NetworkContext(Network(8080))).ToList();

        var detection = Assert.Single(detections);
        Assert.Equal("NET-001", detection.RuleId);
    }

    [Fact]
    public void SuspiciousNetworkPortRule_IgnoresUnconfiguredPort()
    {
        var rule = new SuspiciousNetworkPortRule(
            Options(new DetectionOptions
            {
                SuspiciousNetworkPorts = [8080]
            }));

        var detections = rule.Evaluate(
            NetworkContext(Network(1337))).ToList();

        Assert.Empty(detections);
    }

    [Fact]
    public void SuspiciousNetworkPortRule_WhenDisabled_ProducesNoDetection()
    {
        var rule = new SuspiciousNetworkPortRule(
            Options(new DetectionOptions
            {
                SuspiciousNetworkPortEnabled = false
            }));

        var detections = rule.Evaluate(
            NetworkContext(Network(1337))).ToList();

        Assert.Empty(detections);
    }

    [Fact]
    public void CorrelationRule_DetectsPowerShellOnConfiguredPort()
    {
        var deviceId = Guid.NewGuid();

        var process = Process(
            "powershell.exe",
            processId: 42,
            deviceId: deviceId);

        var network = Network(
            1337,
            processId: 42,
            deviceId: deviceId);

        var rule = new PowerShellSuspiciousNetworkRule(
            Options(new DetectionOptions
            {
                SuspiciousNetworkPorts = [4444, 1337, 31337]
            }));

        var detections = rule.Evaluate(
            new DetectionContext([process], [network])).ToList();

        var detection = Assert.Single(detections);
        Assert.Equal("CORR-001", detection.RuleId);
        Assert.Equal(process.Id, detection.ProcessEventId);
        Assert.Equal(network.Id, detection.NetworkEventId);
    }


    [Fact]
    public void CorrelationRule_IgnoresEventsFromDifferentDevices()
    {
        var options = Options(new DetectionOptions
        {
            PowerShellSuspiciousNetworkEnabled = true,
            SuspiciousNetworkPorts = [4444, 1337, 31337]
        });

        var process = Process(
            "powershell.exe",
            processId: 42,
            deviceId: Guid.NewGuid());

        var network = Network(
            1337,
            processId: 42,
            deviceId: Guid.NewGuid());

        var rule = new PowerShellSuspiciousNetworkRule(options);
        var context = new DetectionContext([process], [network]);

        Assert.Empty(rule.Evaluate(context));
    }


    [Fact]
    public void CorrelationRule_IgnoresDifferentProcessIds()
    {
        var process = Process("powershell.exe", processId: 42);
        var network = Network(1337, processId: 99);

        var rule = new PowerShellSuspiciousNetworkRule(
            Options(new DetectionOptions()));

        var detections = rule.Evaluate(
            new DetectionContext([process], [network])).ToList();

        Assert.Empty(detections);
    }

    [Fact]
    public void CorrelationRule_WhenDisabled_ProducesNoDetection()
    {
        var process = Process("powershell.exe", processId: 42);
        var network = Network(1337, processId: 42);

        var rule = new PowerShellSuspiciousNetworkRule(
            Options(new DetectionOptions
            {
                PowerShellSuspiciousNetworkEnabled = false
            }));

        var detections = rule.Evaluate(
            new DetectionContext([process], [network])).ToList();

        Assert.Empty(detections);
    }
}
