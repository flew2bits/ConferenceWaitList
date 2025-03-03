using System.Collections.Immutable;
using ConferenceBooking.Entities.Users;
using Marten;
using Marten.Events.Aggregation;
using Marten.Schema;

namespace ConferenceBooking.Entities.Sessions.Views;

public record WaitListEntry(Guid UserId, string Email);

public record SessionMembers([property:Identity] Guid SessionId, string Title, 
    ImmutableDictionary<Guid, string> Users,
    ImmutableArray<WaitListEntry> WaitList);

public class SessionMembersProjection : SingleStreamProjection<SessionMembers>
{
    public static SessionMembers Create(SessionCreated evt) => new(evt.SessionId, evt.Title, 
        ImmutableDictionary<Guid, string>.Empty, []);

    public static async Task<SessionMembers> Apply(SeatReserved evt, SessionMembers view, IQuerySession session)
    {
        var user = (await session.LoadAsync<User>(evt.UserId))!;
        return view with { Users = view.Users.Add(user.UserId, user.Email) };
    }

    public static SessionMembers Apply(ReservationCancelled evt, SessionMembers view) =>
        view with { Users = view.Users.Remove(evt.UserId) };

    public static SessionMembers Apply(WaitlistedSeatReleased evt, SessionMembers view) =>
        view with { WaitList = [..view.WaitList.Where(w => w.UserId != evt.UserId)] };

    public static async Task<SessionMembers> Apply(UserWaitlisted evt, SessionMembers view, IQuerySession session)
    {
        var user = (await session.LoadAsync<User>(evt.UserId))!;
        return view with { WaitList = view.WaitList.Add(new WaitListEntry(user.UserId, user.Email)) };
    }

    public static SessionMembers Apply(TopWaitlistReservationPromoted evt, SessionMembers view) =>
        view with
        {
            Users = view.Users.Add(view.WaitList[0].UserId, view.WaitList[0].Email),
            WaitList = view.WaitList[1..]
        };
}