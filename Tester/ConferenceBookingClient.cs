using System.Net;
using System.Net.Http.Json;
using ConferenceBooking;
using ConferenceBooking.Entities.Sessions;
using ConferenceBooking.Entities.Users;

namespace Tester;

public class ConferenceBookingClient(HttpClient client)
{
    private readonly HttpClient _client = client;

    public async Task RegisterNewUser(string email, string name)
    {
        var command = new RegisterNewUser(email.ToUserGuid(), email, name);
        await _client.PostAsJsonAsync("/api/register", command);
    }

    public async Task<ReserveResult> TryToReserveSeat(Guid sessionId, Guid userId)
    {
        var command = new TryReserveSeat(sessionId, userId);
        var response = await _client.PostAsJsonAsync("/api/reserveSeat", command);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var errorResult = await response.Content.ReadFromJsonAsync<ReservationError?>();
            if (errorResult is null or ReservationError.None) return ReserveResult.Error;
            return errorResult == ReservationError.NoLock ? ReserveResult.NoLock : ReserveResult.AlreadyOnList;
        }
        if (response.StatusCode != HttpStatusCode.Accepted) return ReserveResult.Error;
        var result = await response.Content.ReadFromJsonAsync<ReservationClientResult>();
        if (result is null) return ReserveResult.Error;
        return result.Reserved ? ReserveResult.Reserved : ReserveResult.Waitlisted;
    }

    public async Task<bool> CancelReservation(Guid sessionId, Guid userId)
    {
        var command = new CancelReservation(sessionId, userId);
        var response = await _client.PostAsJsonAsync("/api/cancelReservation", command);
        return response.IsSuccessStatusCode;
    }

    public async Task CreateSession(Guid sessionId, string title, int seats, DateTimeOffset startTime)
    {
        var command = new CreateSession(sessionId, title, seats, startTime);
        await _client.PostAsJsonAsync("/api/createSession", command);
    }
}


public enum ReserveResult
{
    NoLock,
    AlreadyOnList,
    Reserved,
    Waitlisted,
    Error
}