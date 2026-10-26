using Microsoft.Extensions.Configuration;

namespace NAITSSMS.Application.Configuration;

/// <summary>
/// Resolves JWT settings with environment variables preferred over appsettings.
/// K8s supplies env JWT__Secret; local dev uses `dotnet user-secrets` (JWT:Secret).
/// Avoid a machine-wide JWT__Secret: it overrides every project's user-secrets.
/// </summary>
public static class JwtConfiguration
{
    public static string ResolveSecret(IConfiguration configuration)
    {
        var secret =
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("JWT__Secret"),
                Environment.GetEnvironmentVariable("JWT_SECRET"),
                Environment.GetEnvironmentVariable("Parameters__JWT_SECRET"),
                configuration["JWT:Secret"],
                configuration["Parameters:JWT_SECRET"]);

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT secret is not configured. Locally run: dotnet user-secrets set \"JWT:Secret\" \"<value>\" " +
                "in NAITSSMS.API; in Kubernetes set the JWT__Secret environment variable from the Secret.");
        }

        return secret;
    }

    public static string? ResolveAudience(IConfiguration configuration) =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("JWT__ValidAudience"),
            configuration["JWT:ValidAudience"]);

    public static string? ResolveIssuer(IConfiguration configuration) =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("JWT__ValidIssuer"),
            configuration["JWT:ValidIssuer"]);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
