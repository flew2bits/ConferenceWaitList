using Marten.Events.Aggregation;
using Marten.Schema;

namespace ConferenceBooking.Entities.Sessions.Views;

public record SessionListItem([property: Identity] Guid SessionId, string Name, int Seats, int Reservations, int WaitlistSize);

public class SessionList : SingleStreamProjection<SessionListItem>
{

    public static SessionListItem Create(SessionCreated evt) => new(evt.SessionId, evt.Title, evt.Seats, 0, 0);

    public static SessionListItem Apply(SeatReserved _, SessionListItem view) =>
        view with { Reservations = view.Reservations + 1 };

    public static SessionListItem Apply(UserWaitlisted _, SessionListItem view) =>
        view with { WaitlistSize = view.WaitlistSize + 1 };

    public static SessionListItem Apply(ReservationCancelled _, SessionListItem view) =>
        view with { Reservations = view.Reservations - 1 };

    public static SessionListItem Apply(WaitlistedSeatReleased _, SessionListItem view) =>
        view with { WaitlistSize = view.WaitlistSize - 1 };

    public static SessionListItem Apply(TopWaitlistReservationPromoted _, SessionListItem view) =>
        view with { WaitlistSize = view.WaitlistSize - 1, Reservations = view.Reservations + 1 };
}