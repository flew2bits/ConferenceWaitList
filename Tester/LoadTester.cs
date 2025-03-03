using ConferenceBooking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tester;

public class LoadTester(
    ConferenceBookingClient client,
    IHostApplicationLifetime applicationLifetime,
    IServiceProvider services)
    : BackgroundService
{
    private static readonly (string Email, string Name)[] Users =
    [
        ("a@example.com", "Albert"),
        ("b@example.com", "Bertrand"),
        ("c@example.com", "Carrie"),
        ("d@example.com", "Darius"),
        ("e@example.com", "Emily"),
        ("f@example.com", "Felicia"),
        ("g@example.com", "Gertrude"),
        ("h@example.com", "Harry"),
        ("i@example.com", "Isaac"),
        ("j@example.com", "Jenny"),
        ("k@examply.com", "Kevin"),
        ("l@example.com", "Lauren"),
        ("m@example.com", "Michael"),
        ("n@example.com", "Nadia"),
        ("o@exmplie.com", "Oliver"),
        ("p@example.com", "Petra"),
        ("q@example.com", "Quinn"),
        ("r@example.com", "Rajesh"),
        ("s@example.com", "Stella"),
        ("t@example.com", "Timothy"),
        ("u@example.com", "Uriah"),
        ("v@example.com", "Veronica"),
        ("w@examply.com", "Wilma"),
        ("x@example.com", "Xavier"),
        ("y@example.com", "Yolanda"),
        ("z@example.com", "Zilla")
    ];

    private static readonly (Guid SessionId, string Title)[] TestSessions =
    [
        ..Enumerable.Range(1, 200).Select(i => $"Test Session {i}")
            .Select(s => (s.ToGuidV5(Constants.ApplicationNamespace), s))
    ];

    private readonly ConferenceBookingClient _client = client;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(async () =>
        {
            await RegisterUsers();
            await CreateSessions();

            var clientConfigs = Users.Select((u, i) =>
                new ApiClientConfig(u.Email.ToUserGuid(),
                    (1.0 + ((Random.Shared.NextDouble() + Random.Shared.NextDouble() + Random.Shared.NextDouble()) / 3.0 -
                         0.5)) / 5.0, TimeSpan.FromMilliseconds(100 + Random.Shared.Next(100) * i),
                    TimeSpan.FromMinutes(3))
            );

            var sessionIds = TestSessions.Select(s => s.SessionId).ToArray();

            await Parallel.ForEachAsync(clientConfigs, stoppingToken, async (config, token) =>
            {
                await using var scope = services.CreateAsyncScope();
                var client = scope.ServiceProvider.GetRequiredService<ConferenceBookingClient>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApiLoadClient>>();
                var tester = new ApiLoadClient(client, config, logger, sessionIds);

                await tester.RunLoadTestAsync(token);
            });

            applicationLifetime.StopApplication();
        }, stoppingToken);

    private async Task RegisterUsers()
    {
        foreach (var (email, name) in Users)
            await _client.RegisterNewUser(email, name);
    }

    private async Task CreateSessions()
    {
        foreach (var (sessionId, title) in TestSessions)
            await _client.CreateSession(sessionId, title, 5, DateTimeOffset.Now);
    }
}