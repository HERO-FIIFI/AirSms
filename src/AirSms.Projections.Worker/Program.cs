using AirSms.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration,
    InfrastructureHostedServices.KafkaConsumer);

var app = builder.Build();
await app.RunAsync();
