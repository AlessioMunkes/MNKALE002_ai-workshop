using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Flash"] = "You are signed out.";
        TempData["FlashKind"] = "info";
        return RedirectToPage("/Index");
    }
}
