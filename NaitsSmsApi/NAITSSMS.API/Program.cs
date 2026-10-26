using CustRewardMgtSys.Application.IService;
using CustRewardMgtSys.Application.Service;
using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using NAITSSMS.API.Services;
using NAITSSMS.Application.Configuration;
using NAITSSMS.Application.IService;
using NAITSSMS.Application.Service;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Infrastructure.Data;
using NAITSSMS.Infrastructure.Repo;
using NAITSSMS.Infrastructure.Unitofwork;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var useRemoteAsmsDb =
    string.Equals(builder.Configuration["NAITS_USE_REMOTE_ASMS_DB"], "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("NAITS_USE_REMOTE_ASMS_DB"), "true", StringComparison.OrdinalIgnoreCase);

if (useRemoteAsmsDb)
{
    builder.Configuration.AddJsonFile("appsettings.Development.RemoteAsmsDb.json", optional: false, reloadOnChange: true);
    Console.WriteLine("WARNING: NAITS_USE_REMOTE_ASMS_DB is enabled — API is using the online ASMS database via localhost:15432.");
}

// .NET Aspire service defaults: OpenTelemetry, health checks, service discovery, HttpClient resilience
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
}

builder.Services.AddMemoryCache();

// Shared image: ASMS/Hills inject ConnectionStrings__NaitsASMSDb; local Aspire and Ama
// may inject NaitsASMSDb (Ama k8s sets both to the same instance connection string).
var npgsqlConnectionName =
    !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("NaitsASMSDb"))
        ? "NaitsASMSDb"
        : !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("NaitsASMSDb"))
            ? "NaitsASMSDb"
            : "NaitsASMSDb";

builder.AddNpgsqlDbContext<AppDbContext>(npgsqlConnectionName, settings =>
{
    // Aspire AppHost already WaitFor(db). DbContext health checks on /health hang/cancel
    // under the debugger (TaskCanceledException) when probes abort a slow ping.
    settings.DisableHealthChecks = true;
});

// Register Identity Services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    var maxFailed = Math.Max(1, builder.Configuration.GetValue("LoginProtection:MaxFailedAttempts", 5));
    var lockoutMinutes = Math.Max(1, builder.Configuration.GetValue("LoginProtection:LockoutMinutes", 30));
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = maxFailed;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
});

// Configure JWT Authentication (JWT__Secret / Parameters__JWT_SECRET env preferred over appsettings)
var jwtSecret = JwtConfiguration.ResolveSecret(builder.Configuration);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = JwtConfiguration.ResolveAudience(builder.Configuration),
        ValidIssuer = JwtConfiguration.ResolveIssuer(builder.Configuration),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

// Configure Controllers with Views (keep JSON options aligned with API controllers)
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Configure AutoMapper - scans MappingProfile assembly
builder.Services.AddAutoMapper(typeof(NAITSSMS.Application.Mappings.MappingProfile).Assembly);

// Register Application Services
builder.Services.AddHttpClient();
builder.Services.Configure<NAITSSMS.Application.Service.EmailCredentials>(
    builder.Configuration.GetSection("EmailCredentials"));

builder.Services.Configure<NAITSSMS.Application.Service.SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailServiceExtension, EmailServiceExtension>();
builder.Services.AddScoped<IRegistrySmsService>();
builder.Services.AddSingleton<ILoginThrottleService, LoginThrottleService>();
builder.Services.AddSingleton<ITwoFactorLoginTicketService, TwoFactorLoginTicketService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITeachersAndOthersService, TeachersAndOthersService>();
builder.Services.AddScoped<ISchoolScopeResolver, SchoolScopeResolver>();
builder.Services.AddScoped<IClassAndArmService, ClassAndArmService>();
builder.Services.AddScoped<IDailyAttendanceService, DailyAttendanceService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();
builder.Services.AddScoped<ISessionAndTermService, SessionAndTermService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<ISecurityExtensionService, SecurityExtensionService>();
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();

builder.Services.AddHttpContextAccessor();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "naitsPortalUi",
         policy =>
        {
            var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "http://82.29.173.232:30888",
                "http://localhost:4209",
                "http://localhost:8088",
                "http://127.0.0.1:8088",
                "http://localhost:8090",
                "https://ourschoolonline.ng",
                "https://www.ourschoolonline.ng",
                "http://ourschoolonline.ng",
                "http://www.ourschoolonline.ng",
                "https://hills.ourschoolonline.ng",
                "https://www.hills.ourschoolonline.ng",
                "http://hills.ourschoolonline.ng",
                "http://www.hills.ourschoolonline.ng"
            };
            foreach (var origin in builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
            {
                if (!string.IsNullOrWhiteSpace(origin))
                {
                    allowedOrigins.Add(origin.Trim());
                }
            }

            if (builder.Configuration.GetValue("Cors:AllowLanOrigins", false))
            {
                policy.SetIsOriginAllowed(origin =>
                    !string.IsNullOrWhiteSpace(origin)
                    && (allowedOrigins.Contains(origin) || IsLanOrLoopbackOrigin(origin)));
            }
            else
            {
                policy.WithOrigins([.. allowedOrigins]);
            }

            policy
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

var app = builder.Build();

// Run the demo seeder against whatever connection string is configured, then exit. Lets us seed
// the VPS database over its Postgres NodePort without shell access to the cluster.
if (args.Contains("--seed-almajiri", StringComparer.OrdinalIgnoreCase))
{
    var seedReport = await AlmajiriDemoSeeder.SeedAsync(app.Services);
    Console.WriteLine(seedReport.Message);
    foreach (var login in seedReport.Logins)
    {
        Console.WriteLine(login);
    }

    return;
}

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        if (useRemoteAsmsDb)
        {
            // Live ASMS database — never run EF migrations or demo seeding against it.
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
                await SchemaRepair.ApplyAsync(db);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    """
                    Remote ASMS database is unreachable on localhost:15432.
                    Open the tunnel and leave that window running:
                      powershell -ExecutionPolicy Bypass -File NaitsSmsApi/scripts/remote-asms-db-tunnel.ps1
                    """,
                    ex);
            }
        }
        else
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Production databases were restored from the legacy system, so migrations are not applied
    // here. SchemaRepair carries the idempotent DDL new tables need. A failure must never
    // stop the API from serving.
    using var schemaScope = app.Services.CreateScope();
    try
    {
        var db = schemaScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SchemaRepair.ApplyAsync(db);
    }
    catch (Exception ex)
    {
        schemaScope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SchemaRepair")
            .LogError(ex, "Schema repair failed on startup; continuing without it.");
    }
}

// Client/probe disconnects cancel in-flight work; do not surface as 500s in the debugger.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException) when (
        context.RequestAborted.IsCancellationRequested
        || context.Request.Path.StartsWithSegments("/health")
        || context.Request.Path.StartsWithSegments("/alive"))
    {
        // Health probes and aborted clients — not application failures.
    }
});

// CORS must run before HTTPS redirection so OPTIONS preflight is not redirected (browsers reject that).
app.UseCors("naitsPortalUi");

if (!app.Configuration.GetValue("DisableHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

// Dynamic API/HTML endpoints must not be browser-cached (full refresh must hit fresh data).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isDynamic =
            path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/home", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/user", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase);

        if (isDynamic)
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null &&
        (path.StartsWith("/Content/SchLogos/", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/uploads/school-logos/", StringComparison.OrdinalIgnoreCase)))
    {
        var fileName = Path.GetFileName(path);
        context.Request.Path = $"/school-logos/{fileName}";
        path = context.Request.Path.Value;
    }
    else if (path != null &&
        path.StartsWith("/uploads/profile-avatars/", StringComparison.OrdinalIgnoreCase))
    {
        var fileName = Path.GetFileName(path);
        context.Request.Path = $"/profile-avatars/{fileName}";
        path = context.Request.Path.Value;
    }
    else if (path != null &&
        (path.StartsWith("/Content/Signatures/", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/uploads/school-head-signatures/", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/uploads/teacher-signatures/", StringComparison.OrdinalIgnoreCase)
         || path.StartsWith("/uploads/signatures/", StringComparison.OrdinalIgnoreCase)))
    {
        // All signatures are stored under wwwroot/Signatures.
        var fileName = Path.GetFileName(path);
        context.Request.Path = $"/Signatures/{fileName}";
        path = context.Request.Path.Value;
    }

    // Linux volumes are case-sensitive; DB often stores .JPG/.PNG while files are .jpg/.png.
    if (path != null)
    {
        var mediaFolders = new[] { "/Passport/", "/Signatures/", "/school-logos/", "/profile-avatars/" };
        foreach (var folder in mediaFolders)
        {
            if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                break;
            }

            var webRoot = app.Environment.WebRootPath;
            var directory = Path.Combine(webRoot, folder.Trim('/'));
            var exactPath = Path.Combine(directory, fileName);
            if (!Directory.Exists(directory) || File.Exists(exactPath))
            {
                break;
            }

            // 1) Case-only mismatch (.PNG vs .png).
            var match = Directory.EnumerateFiles(directory)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

            // 2) Legacy DB truncation: SignatureImageString was often capped (~40 chars)
            // while disk kept a longer name (e.g. guid + "xxxxxxxxxxxxx" + .PNG).
            if (match == null)
            {
                var stem = Path.GetFileNameWithoutExtension(fileName);
                if (!string.IsNullOrWhiteSpace(stem) && stem.Length >= 32)
                {
                    match = Directory.EnumerateFiles(directory)
                        .FirstOrDefault(f =>
                        {
                            var candidateStem = Path.GetFileNameWithoutExtension(f);
                            return candidateStem.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                                   && candidateStem.Length >= stem.Length;
                        });
                }
            }

            if (match != null)
            {
                context.Request.Path = $"{folder}{Path.GetFileName(match)}";
            }
            else if (string.Equals(folder, "/Signatures/", StringComparison.OrdinalIgnoreCase))
            {
                var placeholderPath = Path.Combine(directory, "signature-missing.png");
                if (File.Exists(placeholderPath))
                {
                    context.Request.Path = "/Signatures/signature-missing.png";
                }
            }

            break;
        }
    }

    await next();
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool IsLanOrLoopbackOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
    {
        return false;
    }

    if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!IPAddress.TryParse(uri.Host, out var ip))
    {
        return false;
    }

    if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal)
    {
        return true;
    }

    if (ip.AddressFamily != AddressFamily.InterNetwork)
    {
        return false;
    }

    var bytes = ip.GetAddressBytes();
    return bytes[0] == 10
        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        || (bytes[0] == 192 && bytes[1] == 168);
}
