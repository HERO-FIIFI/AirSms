using System.Text.Json.Serialization;
using AirSms.Api.Authentication;
using AirSms.Api.ErrorHandling;
using AirSms.Api.RateLimiting;
using AirSms.Application.Authentication;
using AirSms.Application.Incidents.Commands.AssignIncident;
using AirSms.Application.Incidents.Commands.CloseIncident;
using AirSms.Application.Incidents.Commands.CreateIncident;
using AirSms.Application.Incidents.Commands.ResolveIncident;
using AirSms.Application.Incidents.Commands.StartIncident;
using AirSms.Application.Incidents.Queries.GetIncidentById;
using AirSms.Application.Incidents.Queries.ListIncidents;
using AirSms.Application.Users;
using AirSms.Domain.Enums;
using AirSms.Infrastructure;
using AirSms.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<CreateIncidentCommandHandler>();
builder.Services.AddScoped<AssignIncidentCommandHandler>();
builder.Services.AddScoped<StartIncidentCommandHandler>();
builder.Services.AddScoped<ResolveIncidentCommandHandler>();
builder.Services.AddScoped<CloseIncidentCommandHandler>();
builder.Services.AddScoped<GetIncidentByIdQueryHandler>();
builder.Services.AddScoped<ListIncidentsQueryHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAirSmsRateLimiting(builder.Configuration);
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
var seedUsers = args.Contains("--seed-users", StringComparer.OrdinalIgnoreCase)
    || builder.Configuration.GetValue<bool>("SeedUsers:Enabled");
if (migrateOnly || bootstrapAdmin || seedUsers
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
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

    if (seedUsers)
    {
        var bootstrap = scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();
        var accounts = builder.Configuration
            .GetSection("SeedUsers:Accounts")
            .Get<SeedUserOptions[]>() ?? [];

        foreach (var account in accounts)
        {
            if (!Enum.TryParse<UserRole>(account.Role, ignoreCase: true, out var role))
            {
                throw new InvalidOperationException(
                    $"Seed user '{account.Email}' has unknown role '{account.Role}'.");
            }

            await bootstrap.EnsureUserAsync(
                new RegisterUserRequest(
                    account.Email,
                    account.Password,
                    account.FirstName,
                    account.LastName),
                role);
        }

        app.Logger.LogInformation("Seeded {SeedUserCount} user account(s).", accounts.Length);
    }

    if (migrateOnly || bootstrapAdmin)
    {
        return;
    }
}

// Behind Caddy every request arrives from the proxy's address. Honour
// X-Forwarded-* only when the deployment says the proxy is the sole ingress,
// otherwise rate limiting would key every user on one IP.
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustProxy"))
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwarded.KnownNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
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
app.UseRateLimiter();

// Container and probe traffic must never be throttled out of its own health check.
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
    .DisableRateLimiting();
app.MapGet("/health/ready", async (AirSmsDbContext dbContext) =>
    await dbContext.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ready" })
        : Results.Problem("AirSms database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable))
    .DisableRateLimiting();

app.MapControllers();

await app.RunAsync();

public partial class Program;

public sealed record SeedUserOptions(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role);
