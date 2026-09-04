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
using Microsoft.AspNetCore.DataProtection;

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

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("ViteDevelopment");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
