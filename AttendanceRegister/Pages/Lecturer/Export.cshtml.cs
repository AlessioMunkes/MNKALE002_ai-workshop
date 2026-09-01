using AttendanceRegister.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceRegister.Pages.Lecturer;

/// <summary>
/// Writes the whole register back out in the same wide layout it is imported
/// from, so a round trip through Excel never loses its shape.
/// </summary>
public class ExportModel : PageModel
{
    private readonly IAttendanceExportService _export;

    public ExportModel(IAttendanceExportService export) => _export = export;

    public async Task<IActionResult> OnGetAsync()
    {
        var file = await _export.ExportRegisterAsync(HttpContext.RequestAborted);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
