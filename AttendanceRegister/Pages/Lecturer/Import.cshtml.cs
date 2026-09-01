using System.ComponentModel.DataAnnotations;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Import;
using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Lecturer;

public class ImportModel : PageModel
{
    private const long MaxBytes = 10L * 1024 * 1024;

    private readonly IAttendanceImportService _import;
    private readonly ICurrentUser _currentUser;

    public ImportModel(IAttendanceImportService import, ICurrentUser currentUser)
    {
        _import = import;
        _currentUser = currentUser;
    }

    [BindProperty]
    [Display(Name = "Spreadsheet")]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public bool CreateMissingStudents { get; set; } = true;

    [BindProperty]
    public bool CreateMissingLectures { get; set; } = true;

    public AttendanceImportResult? Result { get; private set; }

    public IReadOnlyCollection<string> SupportedExtensions => _import.SupportedExtensions;

    public string AcceptList => string.Join(",", SupportedExtensions);

    public void OnGet() { }

    public Task<IActionResult> OnPostValidateAsync() => RunAsync(validateOnly: true);

    public Task<IActionResult> OnPostImportAsync() => RunAsync(validateOnly: false);

    private async Task<IActionResult> RunAsync(bool validateOnly)
    {
        if (Upload is null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a file to upload.");
            return Page();
        }

        if (Upload.Length > MaxBytes)
        {
            ModelState.AddModelError(nameof(Upload),
                $"That file is {Upload.Length / 1024 / 1024} MB. The limit is 10 MB — split it into smaller files or remove unused columns.");
            return Page();
        }

        var extension = Path.GetExtension(Upload.FileName);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Upload),
                $"'{extension}' files cannot be read. Save the sheet as one of: {string.Join(", ", SupportedExtensions)}.");
            return Page();
        }

        await using var stream = Upload.OpenReadStream();

        Result = await _import.ImportAsync(new AttendanceImportRequest
        {
            Content = stream,
            FileName = Upload.FileName,
            CreateMissingStudents = CreateMissingStudents,
            CreateMissingLectures = CreateMissingLectures,
            ValidateOnly = validateOnly,
            PerformedByUserId = _currentUser.UserId
        }, HttpContext.RequestAborted);

        if (Result.Committed)
        {
            TempData["Flash"] = $"Imported {Result.FileName}. {Result.RecordsCreated} added, {Result.RecordsUpdated} changed.";
            TempData["FlashKind"] = "success";
        }

        return Page();
    }
}
