using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mersad.Api.Security;

public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settingName = Scheme.Name switch
        {
            "DashboardKey" => "MersadSecurity:DashboardApiKey",
            "AgentKey" => "MersadSecurity:AgentApiKey",
            _ => null
        };

        if (settingName is null)
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Unknown authentication scheme."));
        }

        var expectedKey = _configuration[settingName];

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            Logger.LogError(
                "Required API key configuration {SettingName} is missing.",
                settingName);

            return Task.FromResult(
                AuthenticateResult.Fail("API authentication is not configured."));
        }

        var authorization = Request.Headers.Authorization.ToString();

        if (!authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var suppliedKey = authorization["Bearer ".Length..].Trim();

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);

        var valid = expectedBytes.Length == suppliedBytes.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        expectedBytes,
                        suppliedBytes);

        if (!valid)
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, Scheme.Name),
            new Claim(ClaimTypes.Role,
                Scheme.Name == "DashboardKey" ? "Dashboard" : "Agent")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
