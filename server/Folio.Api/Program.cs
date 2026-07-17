using System.Text.Json.Serialization;
using Folio.Infrastructure;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(ClientCorsPolicy);

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));

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

/// <summary>Health probe payload.</summary>
public record HealthResponse(string Status);

/// <summary>Exposed so integration tests can spin up the API via WebApplicationFactory.</summary>
public partial class Program;
