using Marten.Schema;
using ConferenceBooking.RedisLocking;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

namespace ConferenceBooking.Entities.Sessions;

public enum SeatType
{
    Reserved,
    Waitlisted
}

public record CancelReservation([property: Identity] Guid SessionId, Guid UserId);

public record CancelContext(LockToken? Token, SeatType SeatType);

public static class CancelReservationEndpoint
{
    public static async Task<(IResult, CancelContext?)> Before(CancelReservation cmd, Session? session, IRedisLockService<Session> lockService)
    {
        if (session is null) return (Results.NotFound(), null);

        var isReserved = session.Reservations.Any(r => r.UserId == cmd.UserId);
        var isWaitlisted = session.WaitList.Any(w => w.UserId == cmd.UserId);

        if (!isReserved && !isWaitlisted) return (Results.NotFound(), null);
        if (isReserved && isWaitlisted) throw new InvalidOperationException("An invalid state has been detected");

        var ctx = new CancelContext(null, isReserved ? SeatType.Reserved : SeatType.Waitlisted);

        if (isWaitlisted) return (WolverineContinue.Result(), ctx);

        var lockResult =
            await lockService.TryAcquireLockWithExponentialBackoffAsync($"session:{session.Id}", TimeSpan.FromSeconds(5), 
                5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        
        return lockResult is null ? (Results.Conflict("Could not get session lock"), null) : (WolverineContinue.Result(), 
            ctx with {Token = lockResult});
    }

    [WolverinePost("/api/cancelReservation", RouteName = "CancelReservation")]
    [AggregateHandler]
    public static (Events, OutgoingMessages, IResult) Post(CancelReservation cmd,
        Session session, CancelContext cancelContext)
        => cancelContext.SeatType == SeatType.Waitlisted
            ? ([new WaitlistedSeatReleased(cmd.SessionId, cmd.UserId)], [], Results.Accepted())
            : ([new ReservationCancelled(cmd.SessionId, cmd.UserId)], session.WaitList.Any()
                ? [new TryPromoteFromWaitlist(cmd.SessionId, cancelContext.Token!)]
                : [], Results.Accepted());

    public static async Task After(CancelContext ctx, OutgoingMessages outgoingMessages, IRedisLockService<Session> lockService)
    {
        if (ctx.Token is null || outgoingMessages.Any()) return;
        await lockService.ReleaseLockAsync(ctx.Token);
    }
}

public record WaitlistedSeatReleased(Guid SessionId, Guid UserId);
public record ReservationCancelled(Guid SessionId, Guid UserId);

public partial record Session
{
    public static Session Apply(WaitlistedSeatReleased evt, Session session) =>
        session with { WaitList = [..session.WaitList.Where(w => w.UserId != evt.UserId)] };

    public static Session Apply(ReservationCancelled evt, Session session) =>
        session with { Reservations = [..session.Reservations.Where(w => w.UserId != evt.UserId)] };
}