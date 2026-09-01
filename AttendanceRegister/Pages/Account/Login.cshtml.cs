using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Data;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAccountService _accounts;
    private readonly AttendanceDbContext _db;
    private readonly SeedOptions _seed;
    private readonly IWebHostEnvironment _environment;

    public LoginModel(IAccountService accounts, AttendanceDbContext db,
        IOptions<SeedOptions> seed, IWebHostEnvironment environment)
    {
        _accounts = accounts;
        _db = db;
        _seed = seed.Value;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool ShowDemoCredentials { get; private set; }
    public string DemoLecturerEmail => _seed.LecturerEmail;
    public string DemoLecturerPassword => _seed.LecturerPassword;
    public string DemoStudentPassword => _seed.DefaultStudentPassword;
    public string DemoStudentNumber { get; private set; } = "STDNUM001";

    public class InputModel
    {
        [Required(ErrorMessage = "Enter your student number or email address.")]
        [Display(Name = "Student number or email")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync() => await LoadDemoCredentialsAsync();

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            await LoadDemoCredentialsAsync();
            return Page();
        }

        var user = await _accounts.AuthenticateAsync(Input.Identifier, Input.Password, HttpContext.RequestAborted);
        if (user is null)
        {
            // Deliberately vague: naming which half was wrong helps an attacker
            // confirm which accounts exist.
            ModelState.AddModelError(string.Empty,
                "We could not match that student number or email to a password. Check both and try again.");
            await LoadDemoCredentialsAsync();
            return Page();
        }

        var principal = _accounts.BuildPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Input.RememberMe ? 24 * 14 : 6)
            });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return user.Role == UserRole.Lecturer
            ? RedirectToPage("/Lecturer/Index")
            : RedirectToPage("/Student/Index");
    }

    private async Task LoadDemoCredentialsAsync()
    {
        ShowDemoCredentials = _environment.IsDevelopment();
        if (!ShowDemoCredentials)
        {
            return;
        }

        var first = await _db.Students.AsNoTracking()
            .OrderBy(s => s.StudentNumber)
            .Select(s => s.StudentNumber)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (!string.IsNullOrEmpty(first))
        {
            DemoStudentNumber = first;
        }
    }
}
