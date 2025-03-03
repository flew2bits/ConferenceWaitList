using ConferenceBooking.RedisLocking;
using Wolverine.Marten;

namespace ConferenceBooking.Entities.Sessions;

public record TryPromoteFromWaitlist(Guid SessionId, LockToken Token);

public static class TryPromoteFromWaitlistHandler
{
    [AggregateHandler]
    public static Events Handle(TryPromoteFromWaitlist cmd, Session? session)
    {
        if (session is null || !session.WaitList.Any()) return [];

        return [new TopWaitlistReservationPromoted(cmd.SessionId)];
    }

    public static async Task After(TryPromoteFromWaitlist cmd, IRedisLockService<Session> lockService)
    {
        await lockService.ReleaseLockAsync(cmd.Token);
    }
}

public record TopWaitlistReservationPromoted(Guid SessionId);

public partial record Session
{
    public static Session Apply(TopWaitlistReservationPromoted evt, Session session) =>
        session with
        {
            Reservations = session.Reservations.Add(new Reservation(session.WaitList[0].UserId)),
            WaitList = session.WaitList[1..]
        };
}