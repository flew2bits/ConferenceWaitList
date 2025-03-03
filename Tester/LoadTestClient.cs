using Microsoft.Extensions.Logging;
using Tester;

public record ApiClientConfig(Guid UserId, double RequestDelay, TimeSpan StartDelay, TimeSpan Duration);

// Your API client class
public class ApiLoadClient
{
    private readonly ConferenceBookingClient _bookingClient;
    private readonly ApiClientConfig _config;
    private readonly ILogger<ApiLoadClient> _logger;
    private readonly Guid[] _sessionIds;

    private HashSet<Guid> _mySessions = [];

    public ApiLoadClient(ConferenceBookingClient bookingClient, ApiClientConfig config, ILogger<ApiLoadClient> logger, Guid[] sessionIds)
    {
        _bookingClient = bookingClient;
        _config = config;
        _logger = logger;
        _sessionIds = sessionIds;
    }

    public async Task RunLoadTestAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initialized load test for user: {UserId}", _config.UserId);

        await Task.Delay(_config.StartDelay, cancellationToken);

        _logger.LogInformation("Starting load test for user: {UserId}", _config.UserId);
        
        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(_config.Duration);

        var random = new Random();
        
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var iterationStart = DateTime.UtcNow;
            
            try
            {
                // Make your API call here
                var sessionId = _sessionIds[random.Next(_sessionIds.Length)];
                if (_mySessions.Contains(sessionId))
                {
                    var result = await _bookingClient.CancelReservation(sessionId, _config.UserId);
                    if (result) _mySessions.Remove(sessionId);
                    else _logger.LogError("{UserId}: Could not cancel session", _config.UserId);
                }
                else
                {
                    var response = await _bookingClient.TryToReserveSeat(sessionId, _config.UserId);
                    if (response is ReserveResult.Waitlisted or ReserveResult.Reserved)
                    {
                        _mySessions.Add(sessionId);
                        _logger.LogWarning("{UserId}: Reserve Seat Result {Result}", _config.UserId, response.ToString());
                    }
                    else
                    {
                        _logger.LogError("{UserId}: Could not reserve seat {Result}", _config.UserId, response.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{UserId}: Error making request", _config.UserId);
            }
            
            // Throttle to respect the RequestsPerSecond setting
            var iterationTime = DateTime.UtcNow - iterationStart;
            var delay = TimeSpan.FromSeconds(_config.RequestDelay) - iterationTime;
            
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        _logger.LogInformation("Load test completed for user: {UserId}", _config.UserId);
    }
}