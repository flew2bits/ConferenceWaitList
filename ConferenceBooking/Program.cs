using ConferenceBooking.Entities;
using ConferenceBooking.Services;
using ConferenceBooking.Services.Redis;
using Marten;
using Oakton;
using ConferenceBooking.RedisLocking;
using Marten.Events;
using StackExchange.Redis;
using Weasel.Core;
using Wolverine;
using Wolverine.Marten;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder();
builder.UseWolverine(cfg =>
{
    cfg.ApplicationAssembly = typeof(Program).Assembly; 
});

var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);


builder.Services.AddSingleton<IRedisLockService, RedisLockService>();
builder.Services.AddSingleton(typeof(IRedisLockService<>), typeof(RedisLockService<>));
builder.Services.AddTransient<RedisAvailabilityCache>();
builder.Services.AddTransient<SessionAvailabilityService>();
builder.Services.AddTransient<RedisWaitlistService>();
builder.Services.AddTransient<RedisPubSubService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddMarten(cfg =>
    {
        cfg.Connection("Host=localhost; User Name=postgres; Password=psql; Port=5436");
        cfg.AutoCreateSchemaObjects = AutoCreate.All;
        cfg.DisableNpgsqlLogging = true;
        cfg.Events.AppendMode = EventAppendMode.Quick;
    })
    .IntegrateWithWolverine(cfg => cfg.UseFastEventForwarding = true)
    .ApplyAllDatabaseChangesOnStartup()
    .UseLightweightSessions();

builder.AddEntities();


builder.Services.AddRazorPages();
builder.Services.AddWolverineHttp();


var app = builder.Build();
app.MapStaticAssets();
app.MapRazorPages();
app.MapWolverineEndpoints();
await app.RunOaktonCommands(args);