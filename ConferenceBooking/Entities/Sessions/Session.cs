using System.Collections.Immutable;

namespace ConferenceBooking.Entities.Sessions;

/// <summary>
/// Represents a conference session that attendees can register for.
/// </summary>
public partial record Session(
    Guid Id,
    string Title,
    int TotalSeats,
    DateTimeOffset StartTime)
{
    public string Description { get; init; } = string.Empty;

    public string Speaker { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public int DurationMinutes { get; init; } = 60;

    public ImmutableArray<Reservation> Reservations { get; init; } = [];

    public ImmutableArray<WaitlistEntry> WaitList { get; init; } = [];
}

public record Reservation(Guid UserId)
{
    /// <summary>
    /// Optional status field to track the reservation state
    /// </summary>
    public ReservationStatus Status { get; init; } = ReservationStatus.Confirmed;

    /// <summary>
    /// Optional notes or special requirements
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}

public enum ReservationStatus
{
    Confirmed,
    Attended,
    NoShow,
    Cancelled
}

public record WaitlistEntry(Guid UserId, DateTimeOffset JoinedAt)
{
    /// <summary>
    /// Indicates whether the user has been notified about their waitlist position
    /// </summary>
    public bool NotificationSent { get; init; } = false;

    /// <summary>
    /// Timestamp of when notification was sent, if applicable
    /// </summary>
    public DateTimeOffset? NotificationSentAt { get; init; } = null;

    /// <summary>
    /// Status of this waitlist entry
    /// </summary>
    public WaitlistStatus Status { get; init; } = WaitlistStatus.Waiting;

    /// <summary>
    /// Creates a new WaitlistEntry with updated notification information
    /// </summary>
    public WaitlistEntry WithNotification(DateTimeOffset sentAt) =>
        this with { NotificationSent = true, NotificationSentAt = sentAt };

    /// <summary>
    /// Creates a new WaitlistEntry with updated status
    /// </summary>
    public WaitlistEntry WithStatus(WaitlistStatus newStatus) =>
        this with { Status = newStatus };
}

public enum WaitlistStatus
{
    Waiting,
    Promoted,
    Expired,
    Declined
}