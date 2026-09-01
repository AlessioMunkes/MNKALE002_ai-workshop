using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }

    // Named ErrorCode rather than StatusCode: PageModel already exposes a
    // StatusCode(int) helper method, and a property of the same name hides it.
    public int ErrorCode { get; private set; } = 500;

    public string Headline { get; private set; } = "Something went wrong on our side";
    public string Explanation { get; private set; } =
        "The page could not be loaded. Try again, and if it keeps happening tell your course convenor what you were doing at the time.";

    public void OnGet(int? code)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        ErrorCode = code ?? 500;

        switch (ErrorCode)
        {
            case 400:
                Headline = "That request could not be read";
                Explanation = "Something in the form was not in the shape the page expected. Go back, reload the page and try once more.";
                break;
            case 403:
                Headline = "You do not have access to that page";
                Explanation = "Student and lecturer screens are kept separate. Sign in with the right account if you need to be there.";
                break;
            case 404:
                Headline = "That page does not exist";
                Explanation = "The link may be out of date, or the session it pointed at has been removed.";
                break;
            case 405:
                Headline = "That action is not allowed here";
                Explanation = "The page was reached in a way it does not support. Start again from the menu.";
                break;
        }
    }
}
