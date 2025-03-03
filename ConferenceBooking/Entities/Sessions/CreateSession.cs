using Marten.Schema;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Marten;

namespace ConferenceBooking.Entities.Sessions;

public record CreateSession([property: Identity] Guid SessionId, string Title, int Seats, DateTimeOffset StartTime);

public static class CreateSessionHandler
{
    [AggregateHandler]
    [WolverinePost("/api/createSession")]
    public static Events Handle(CreateSession cmd, Session? session)
    {
        if (session is not null) return [];
        if (cmd.Seats < 0) return [];

        return [new SessionCreated(cmd.SessionId, cmd.Title, cmd.Seats, cmd.StartTime)];
    }
}

public record SessionCreated(Guid SessionId, string Title, int Seats, DateTimeOffset StartTime);

public partial record Session
{
    public static Session Create(SessionCreated evt) =>
        new(evt.SessionId, evt.Title, evt.Seats, evt.StartTime);
}