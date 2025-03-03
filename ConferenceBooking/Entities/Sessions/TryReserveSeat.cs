using ConferenceBooking.Entities.Users;
using Marten;
using Marten.Events;
using Marten.Schema;
using ConferenceBooking.RedisLocking;
using Wolverine.Http;
using Wolverine.Marten;

namespace ConferenceBooking.Entities.Sessions;

public record TryReserveSeat([property: Identity] Guid SessionId, Guid UserId);

public static class TryReserveSessionSeatEndpoint
{
    
    
    public static async Task<(IResult, LockToken)> Before(TryReserveSeat cmd,
        IRedisLockService<Session> lockService, Session? session, IQuerySession querySession)
    {
        if (session is null) return (Results.NotFound(), null!);
        var user = await querySession.LoadAsync<User>(cmd.UserId);
        if (user is null) return (Results.NotFound(), null!);

        var lockResult = await lockService.TryAcquireLockWithExponentialBackoffAsync($"{cmd.SessionId}",
            TimeSpan.FromSeconds(5), 5,
            TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1));


        return lockResult is null
            ? (Results.Conflict(ReservationError.NoLock), null!)
            : (WolverineContinue.Result(), lockResult);
    }

    [WolverinePost("/api/reserveSeat", RouteName = "ReserveSeat")]
    [AggregateHandler]
    public static (IResult, Events) Post(TryReserveSeat cmd, Session session)
    {
        if (session.Reservations.Any(r => r.UserId == cmd.UserId) || session.WaitList.Any(r => r.UserId == cmd.UserId))
            return (Results.Conflict(ReservationError.ReservedOrWaitlist), []);

        return session.Reservations.Length < session.TotalSeats
            ? (Results.Accepted(value: new ReservationClientResult(true, "Seat reserved")),
                [new SeatReserved(cmd.SessionId, cmd.UserId)])
            : (Results.Accepted(value: new ReservationClientResult(false, "Added to waitlist")),
                [new UserWaitlisted(cmd.SessionId, cmd.UserId)]);
    }

    public static async Task After(IRedisLockService<Session> lockService, LockToken token) =>
        await lockService.ReleaseLockAsync(token);
}

public record ReservationClientResult(bool Reserved, string Message);

public enum ReservationError
{
    None,
    NoLock,
    ReservedOrWaitlist
}

public record SeatReserved(Guid SessionId, Guid UserId);

public record UserWaitlisted(Guid SessionId, Guid UserId);

public partial record Session
{
    public static Session Apply(SeatReserved evt, Session session) =>
        session with { Reservations = session.Reservations.Add(new Reservation(evt.UserId)) };

    public static Session Apply(IEvent<UserWaitlisted> evt, Session session) =>
        session with { WaitList = session.WaitList.Add(new WaitlistEntry(evt.Data.UserId, evt.Timestamp)) };
}