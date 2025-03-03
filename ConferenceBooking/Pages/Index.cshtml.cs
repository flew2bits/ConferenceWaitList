using ConferenceBooking.Entities.Sessions;
using ConferenceBooking.Entities.Sessions.Views;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wolverine;

namespace ConferenceBooking.Pages;

public class Index : PageModel
{
    public IEnumerable<SessionListItem> Sessions { get; set; } = [];

    public async Task OnGet([FromServices] IQuerySession session)
    {
        Sessions = await session.Query<SessionListItem>().OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateSession(CreateSession cmd,[FromServices] IMessageBus bus)
    {
        await bus.InvokeAsync(cmd with {SessionId = Guid.NewGuid()});
        return RedirectToPage();
    }
}