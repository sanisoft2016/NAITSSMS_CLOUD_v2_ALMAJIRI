var builder = DistributedApplication.CreateBuilder(args);

var useRemoteAsmsDb =
    string.Equals(builder.Configuration["NAITS_USE_REMOTE_ASMS_DB"], "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("NAITS_USE_REMOTE_ASMS_DB"), "true", StringComparison.OrdinalIgnoreCase);

// User-secrets / appsettings use "Parameters:portno".
// Env vars use "Parameters__portno". Configuration covers both.
var usernameValue = builder.Configuration["Parameters:username"] ?? "postgres";
var passwordValue = builder.Configuration["Parameters:password"] ?? "PostgresDev123!";

var username = builder.AddParameter("username", usernameValue);
var password = builder.AddParameter("password", passwordValue, secret: true);

var sqlPortNo = builder.Configuration["Parameters:portno"]
    ?? Environment.GetEnvironmentVariable("Parameters__portno")
    ?? "5433";
var sqlPortNoInInteger = int.Parse(sqlPortNo);

// Local dev: dotnet user-secrets set "Parameters:JWT_SECRET" "<value>" in NAITSSMS.AppHost.
var jwtSecret =
    builder.Configuration["Parameters:JWT_SECRET"]
    ?? Environment.GetEnvironmentVariable("JWT__Secret")
    ?? Environment.GetEnvironmentVariable("Parameters__JWT_SECRET")
    ?? Environment.GetEnvironmentVariable("JWT_SECRET");

var api = builder.AddProject<Projects.NAITSSMS_API>("naitsasms-api")
    .WithExternalHttpEndpoints()
    // Use /alive (self/live only). /health also runs Aspire Npgsql DbContext checks and
    // frequently throws TaskCanceledException when the probe aborts a slow DB ping.
    .WithHttpHealthCheck("/alive");

if (useRemoteAsmsDb)
{
    api.WithEnvironment("NAITS_USE_REMOTE_ASMS_DB", "true");
}
else
{
    var postgres = builder.AddPostgres("naitsServer", username, password, sqlPortNoInInteger)
        .WithImageTag("17.0")
        .WithDataVolume(isReadOnly: false)
        .WithPgAdmin();

    var naitsDb = postgres.AddDatabase("NaitsASMSDb");
    api.WithReference(naitsDb).WaitFor(naitsDb);
}

if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    api.WithEnvironment("JWT__Secret", jwtSecret);
}

builder.Build().Run();
