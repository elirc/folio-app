var builder = WebApplication.CreateBuilder(args);

const string ClientCorsPolicy = "client";

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
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

app.Run();

/// <summary>Health probe payload.</summary>
public record HealthResponse(string Status);

/// <summary>Exposed so integration tests can spin up the API via WebApplicationFactory.</summary>
public partial class Program;
