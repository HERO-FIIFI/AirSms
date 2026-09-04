using System.Security.Claims;
using System.Text;
using AirSms.Notifications.Email;
using AirSms.Notifications.Options;
using AirSms.Notifications.Persistence;
using AirSms.Notifications.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AirSms.Notifications;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
                builder.Environment.ContentRootPath,
                "App_Data",
                "DataProtectionKeys")));

        builder.Services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDatabase")
                ?? throw new InvalidOperationException("Connection string 'NotificationsDatabase' was not found.")));

        builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
        builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
        builder.Services.AddScoped<NotificationEventHandler>();
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.AddHostedService<KafkaNotificationConsumer>();
        builder.Services.AddHostedService<EmailDeliveryWorker>();
        builder.Services.AddProblemDetails();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("ViteDevelopment", policy =>
                policy
                    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        AddJwt(builder);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("ViteDevelopment");
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health/ready", async (NotificationsDbContext dbContext) =>
            await dbContext.Database.CanConnectAsync()
                ? Results.Ok(new { status = "ready" })
                : Results.Problem("Notification database is unavailable.", statusCode: 503));

        MapNotificationEndpoints(app);

        await app.RunAsync();
    }

    private static void AddJwt(WebApplicationBuilder builder)
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        if (string.IsNullOrWhiteSpace(jwt.Issuer) ||
            string.IsNullOrWhiteSpace(jwt.Audience) ||
            string.IsNullOrWhiteSpace(jwt.SigningKey) ||
            Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT configuration is missing or invalid.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role
                };
            });
        builder.Services.AddAuthorization();
    }

    private static void MapNotificationEndpoints(WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications");

        notifications.MapGet("/", ListNotifications).RequireAuthorization();

        notifications.MapGet("/unread-count", UnreadCount).RequireAuthorization();

        notifications.MapPost("/{id:guid}/read", MarkRead).RequireAuthorization();
    }

    private static IResult ListNotifications(
        ClaimsPrincipal user,
        NotificationsDbContext dbContext)
    {
        if (!TryCurrentUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var notifications = dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.UserId,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.RelatedIncidentId,
                notification.CreatedAt,
                notification.ReadAt,
                notification.SourceEventId))
            .ToList();

        return Results.Ok(notifications);
    }

    private static IResult UnreadCount(
        ClaimsPrincipal user,
        NotificationsDbContext dbContext)
    {
        if (!TryCurrentUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new UnreadNotificationCountResponse(dbContext.Notifications.Count(notification =>
            notification.UserId == userId &&
            notification.ReadAt == null)));
    }

    private static async Task<IResult> MarkRead(
        Guid id,
        ClaimsPrincipal user,
        NotificationsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryCurrentUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (notification is null || notification.UserId != userId)
        {
            return Results.NotFound();
        }

        notification.MarkRead();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new NotificationResponse(
            notification.Id,
            notification.UserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.RelatedIncidentId,
            notification.CreatedAt,
            notification.ReadAt,
            notification.SourceEventId));
    }

    private static bool TryCurrentUserId(ClaimsPrincipal user, out Guid userId)
    {
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}

public sealed record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Message,
    Guid? RelatedIncidentId,
    DateTime CreatedAt,
    DateTime? ReadAt,
    Guid SourceEventId);

public sealed record UnreadNotificationCountResponse(int Count);
