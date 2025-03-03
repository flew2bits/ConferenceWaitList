using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tester;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddHttpClient<ConferenceBookingClient>(cfg =>
{
    cfg.BaseAddress = new Uri("https://localhost:7119");
});

builder.Services.AddHostedService<LoadTester>();

var app = builder.Build();

await app.RunAsync();