using System.Text.Json.Serialization;
using Auditart.Infrastructure;
using Auditart.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "auditart-api",
    utc = DateTime.UtcNow
}));

await DbSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;
