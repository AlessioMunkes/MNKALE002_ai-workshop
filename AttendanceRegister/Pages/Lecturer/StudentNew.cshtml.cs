using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Services;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Pages.Lecturer;

public class StudentNewModel : PageModel
{
    private readonly IStudentAdminService _students;
    private readonly CourseOptions _course;

    public StudentNewModel(IStudentAdminService students, IOptions<CourseOptions> course)
    {
        _students = students;
        _course = course.Value;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int MinimumPasswordLength => StudentAdminService.MinimumPasswordLength;
    public string EmailDomain => _course.StudentEmailDomain;
    public string EmailPlaceholder => "mkntha001@" + _course.StudentEmailDomain;

    public class InputModel
    {
        [Required(ErrorMessage = "Enter a student number.")]
        [StringLength(20, ErrorMessage = "Student numbers are at most 20 characters.")]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the student's full name.")]
        [StringLength(200)]
        [Display(Name = "Full name")]
        public string DisplayName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "That does not look like an email address.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Set a starting password for the student.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The starting password needs at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Starting password")]
        public string Password { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var outcome = await _students.AddAsync(Input.StudentNumber, Input.DisplayName, Input.Email,
            Input.Password, HttpContext.RequestAborted);

        if (!outcome.Success)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            return Page();
        }

        TempData["Flash"] = outcome.Message;
        TempData["FlashKind"] = "success";
        return RedirectToPage("/Lecturer/StudentDetail", new { studentId = outcome.StudentId });
    }
}
