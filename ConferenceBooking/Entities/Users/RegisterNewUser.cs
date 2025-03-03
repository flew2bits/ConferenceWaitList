using Marten;
using Marten.Schema;
using Wolverine.Http;
using Wolverine.Marten;

namespace ConferenceBooking.Entities.Users;

public record RegisterNewUser([property: Identity] Guid UserId, string Email, string Name);


public static class RegisterNewUserEndpoint
{
    public static async Task<IEnumerable<User>> LoadAsync(RegisterNewUser cmd, IQuerySession session) =>
        await session.Query<User>()
            .Where(u => u.Email.Equals(cmd.Email, StringComparison.InvariantCultureIgnoreCase) || u.UserId == cmd.UserId).ToListAsync();
    
    [WolverinePost("/api/register", RouteName = "RegisterUser")]
    [AggregateHandler(AggregateType = typeof(User))]
    public static (IResult, Events) Post(RegisterNewUser cmd, IEnumerable<User> existingUsers)
    {
        if (existingUsers.Any()) return (Results.Conflict("A matching user already exists"), []);

        return (Results.Accepted(), [new UserRegistered(cmd.UserId, cmd.Email, cmd.Name)]);
    }
}

public record UserRegistered(Guid UserId, string Email, string Name);

public partial record User
{
    public static User Create(UserRegistered evt) => new(evt.UserId, evt.Email, evt.Name);
}