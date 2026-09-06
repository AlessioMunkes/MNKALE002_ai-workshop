using AttendanceRegister.Data;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Repositories;
using AttendanceRegister.Services;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Import;
using AttendanceRegister.Services.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- options ---
builder.Services.Configure<CourseOptions>(builder.Configuration.GetSection(CourseOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection(BrandingOptions.SectionName));
builder.Services.Configure<CheckInOptions>(builder.Configuration.GetSection(CheckInOptions.SectionName));

// -------------------------------------------------------------- data layer --
var connectionString = builder.Configuration.GetConnectionString("AttendanceDatabase")
                       ?? "Data Source=App_Data/attendance.db";

builder.Services.AddDbContext<AttendanceDbContext>(options =>
{
    options.UseSqlite(connectionString);
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging(false);
    }
});

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ILectureRepository, LectureRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ---------------------------------------------------------------- services --
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddSingleton<ICheckInCodeService, CheckInCodeService>();
builder.Services.AddScoped<ICourseContext, CourseContext>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IStudentAdminService, StudentAdminService>();
builder.Services.AddScoped<ISessionAdminService, SessionAdminService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAttendanceQueryService, AttendanceQueryService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IAttendanceExportService, AttendanceExportService>();

// Strategy pattern: one parser per supported file format, resolved by a factory.
builder.Services.AddScoped<IAttendanceFileParser, DelimitedAttendanceFileParser>();
builder.Services.AddScoped<IAttendanceFileParser, ExcelAttendanceFileParser>();
builder.Services.AddScoped<IAttendanceFileParserFactory, AttendanceFileParserFactory>();
builder.Services.AddScoped<IAttendanceImportService, AttendanceImportService>();

builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---------------------------------------------------- authn / authz / pages --
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(6);
        options.SlidingExpiration = true;
        options.Cookie.Name = "AttendanceRegister.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.StudentOnly,
        policy => policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Student)));
    options.AddPolicy(AuthorizationPolicies.LecturerOnly,
        policy => policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Lecturer)));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Student", AuthorizationPolicies.StudentOnly);
    options.Conventions.AuthorizeFolder("/Lecturer", AuthorizationPolicies.LecturerOnly);
    options.Conventions.AllowAnonymousToFolder("/Account");
});

var app = builder.Build();

// ---------------------------------------------------------------- pipeline --
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// --------------------------------------------------- database bootstrapping --
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.InitialiseAsync();
}

app.Run();
