using System.Text.Json.Serialization;
using AirSms.Api.Authentication;
using AirSms.Api.ErrorHandling;
using AirSms.Application.Authentication;
using AirSms.Application.Incidents.Commands.AssignIncident;
using AirSms.Application.Incidents.Commands.CloseIncident;
using AirSms.Application.Incidents.Commands.CreateIncident;
using AirSms.Application.Incidents.Commands.ResolveIncident;
using AirSms.Application.Incidents.Commands.StartIncident;
using AirSms.Application.Incidents.Queries.GetIncidentById;
using AirSms.Application.Incidents.Queries.ListIncidents;
using AirSms.Infrastructure;
using AirSms.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
        builder.Environment.ContentRootPath,
        "App_Data",
        "DataProtectionKeys")));

builder.Services.AddInfrastructure(
    builder.Configuration,
    InfrastructureHostedServices.OutboxPublisher);
builder.Services.AddAirSmsAuthentication(builder.Configuration);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminBootstrapService>();
builder.Services.AddScoped<CreateIncidentCommandHandler>();
builder.Services.AddScoped<AssignIncidentCommandHandler>();
builder.Services.AddScoped<StartIncidentCommandHandler>();
builder.Services.AddScoped<ResolveIncidentCommandHandler>();
builder.Services.AddScoped<CloseIncidentCommandHandler>();
builder.Services.AddScoped<GetIncidentByIdQueryHandler>();
builder.Services.AddScoped<ListIncidentsQueryHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDevelopment", policy =>
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var bootstrapAdmin = args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase);
if (migrateOnly || bootstrapAdmin || builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AirSmsDbContext>().Database.MigrateAsync();

    if (bootstrapAdmin)
    {
        string Required(string key) => builder.Configuration[key]
            ?? throw new InvalidOperationException($"Configuration '{key}' is required.");

        await scope.ServiceProvider.GetRequiredService<AdminBootstrapService>().EnsureAdminAsync(
            new RegisterUserRequest(
                Required("BootstrapAdmin:Email"),
                Required("BootstrapAdmin:Password"),
                Required("BootstrapAdmin:FirstName"),
                Required("BootstrapAdmin:LastName")));
    }

    if (migrateOnly || bootstrapAdmin)
    {
        return;
    }
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ViteDevelopment");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (AirSmsDbContext dbContext) =>
    await dbContext.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ready" })
        : Results.Problem("AirSms database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapControllers();

await app.RunAsync();

public partial class Program;
