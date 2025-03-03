using Marten.Schema;

namespace ConferenceBooking.Entities.Users;

public partial record User([property:Identity] Guid UserId, string Email, string Name);