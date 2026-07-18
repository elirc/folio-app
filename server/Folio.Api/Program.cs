using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Folio.Api.Auth;
using Folio.Infrastructure;
using Folio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

const string ClientCorsPolicy = "client";

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options =>
{
    // Stamp every ProblemDetails with the request path and a correlation id so
    // clients and logs can be tied together.
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

var connectionString = builder.Configuration.GetConnectionString("Folio")
    ?? "Data Source=folio.db";
builder.Services.AddFolioInfrastructure(connectionString);

builder.Services.AddScoped<Folio.Api.Services.PageService>();
builder.Services.AddScoped<Folio.Api.Services.BlockService>();
builder.Services.AddScoped<Folio.Api.Services.SearchService>();
builder.Services.AddScoped<Folio.Api.Services.PageVersionService>();
builder.Services.AddScoped<Folio.Api.Services.CommentService>();
builder.Services.AddScoped<Folio.Api.Services.LinkService>();
builder.Services.AddScoped<Folio.Api.Services.TemplateService>();
builder.Services.AddScoped<Folio.Api.Services.ActivityService>();
builder.Services.AddScoped<Folio.Api.Services.NotificationService>();

// ---- authentication / authorization ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentMemberAccessor, CurrentMemberAccessor>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// ---- rate limiting (write endpoints only, partitioned per user) ----
var writePermitLimit = builder.Configuration.GetValue<int?>("RateLimit:PermitLimit") ?? 300;
var writeWindowSeconds = builder.Configuration.GetValue<int?>("RateLimit:WindowSeconds") ?? 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var isWrite = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
        // Reads, auth, public-link, and health are never throttled.
        var exempt = path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/health", StringComparison.OrdinalIgnoreCase);
        if (!isWrite || exempt)
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        var key = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter($"write:{key}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = writePermitLimit,
            Window = TimeSpan.FromSeconds(writeWindowSeconds),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Structured per-request logging: method, path, status, and elapsed ms.
app.Use(async (context, next) =>
{
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    await next();
    var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Folio.Requests");
    logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0}ms",
        context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
});

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(ClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness + DB probe with a structured body.
app.MapGet("/health", async (FolioDbContext db, CancellationToken ct) =>
{
    bool dbUp;
    try
    {
        dbUp = await db.Database.CanConnectAsync(ct);
    }
    catch
    {
        dbUp = false;
    }

    var response = new HealthResponse(dbUp ? "ok" : "degraded", dbUp ? "up" : "down", DateTime.UtcNow);
    return dbUp ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapControllers();

// Apply migrations and seed development data at startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FolioDbContext>();
    db.Database.Migrate();
    if (app.Environment.IsDevelopment())
    {
        DbSeeder.Seed(db);
    }
}

app.Run();

/// <summary>Health probe payload: overall status, DB reachability, and a timestamp.</summary>
public record HealthResponse(string Status, string Database, DateTime Timestamp);

/// <summary>Exposed so integration tests can spin up the API via WebApplicationFactory.</summary>
public partial class Program;
