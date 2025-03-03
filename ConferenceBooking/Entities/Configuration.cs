using ConferenceBooking.Entities.Sessions.Views;
using ConferenceBooking.Entities.Users;
using Marten;
using Marten.Events.Projections;

namespace ConferenceBooking.Entities;

public static class Configuration
{
    public static void AddEntities(this IHostApplicationBuilder builder)
    {
        builder.Services.ConfigureMarten(cfg =>
        {
            cfg.Projections.Add<SessionList>(ProjectionLifecycle.Inline);
            cfg.Projections.Add<SessionMembersProjection>(ProjectionLifecycle.Inline);
            cfg.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
        });
    }
}