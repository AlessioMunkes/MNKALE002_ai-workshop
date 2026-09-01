using AttendanceRegister.Data;
using AttendanceRegister.Infrastructure;
using AttendanceRegister.Models.Entities;
using AttendanceRegister.Repositories;
using AttendanceRegister.Services.Abstractions;
using AttendanceRegister.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttendanceRegister.Services;

public sealed class StudentAdminService : IStudentAdminService
{
    public const int MinimumPasswordLength = 8;

    private readonly AttendanceDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CourseOptions _course;

    public StudentAdminService(AttendanceDbContext db, IPasswordHasher hasher, IUnitOfWork unitOfWork,
        IOptions<CourseOptions> course)
    {
        _db = db;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _course = course.Value;
    }

    public string SuggestEmail(string studentNumber) =>
        string.IsNullOrWhiteSpace(studentNumber)
            ? string.Empty
            : $"{Student.NormaliseStudentNumber(studentNumber).ToLowerInvariant()}@{_course.StudentEmailDomain}";

    public async Task<StudentAdminOutcome> AddAsync(string studentNumber, string displayName, string? email,
        string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
        {
            return new StudentAdminOutcome(false, "Enter a student number.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new StudentAdminOutcome(false, "Enter the student's name.");
        }

        if (password.Length < MinimumPasswordLength)
        {
            return new StudentAdminOutcome(false, $"The starting password needs at least {MinimumPasswordLength} characters.");
        }

        var number = Student.NormaliseStudentNumber(studentNumber);
        var address = User.NormaliseEmail(string.IsNullOrWhiteSpace(email) ? SuggestEmail(number) : email);

        if (await _db.Students.AnyAsync(s => s.StudentNumber == number, cancellationToken))
        {
            return new StudentAdminOutcome(false, $"{number} is already enrolled. Open that student instead of adding a second record.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == address, cancellationToken))
        {
            return new StudentAdminOutcome(false, $"{address} is already in use by another account. Give this student a different address.");
        }

        var student = new Student(number, displayName, address, _hasher.Hash(password));
        _db.Students.Add(student);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StudentAdminOutcome(true, $"{student.DisplayName} added to the course.", student.Id);
    }

    public async Task<StudentAdminOutcome> UpdateAsync(int studentId, string studentNumber, string displayName,
        string email, CancellationToken cancellationToken = default)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        if (student is null)
        {
            return new StudentAdminOutcome(false, "That student no longer exists.");
        }

        if (string.IsNullOrWhiteSpace(studentNumber) || string.IsNullOrWhiteSpace(displayName))
        {
            return new StudentAdminOutcome(false, "A student needs both a number and a name.");
        }

        var number = Student.NormaliseStudentNumber(studentNumber);
        var address = User.NormaliseEmail(email);

        if (await _db.Students.AnyAsync(s => s.StudentNumber == number && s.Id != studentId, cancellationToken))
        {
            return new StudentAdminOutcome(false, $"{number} belongs to another student.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == address && u.Id != studentId, cancellationToken))
        {
            return new StudentAdminOutcome(false, $"{address} belongs to another account.");
        }

        student.ChangeStudentNumber(number);
        student.Rename(displayName);
        student.SetEmail(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StudentAdminOutcome(true, "Student details updated.", student.Id);
    }

    public async Task<StudentAdminOutcome> SetActiveAsync(int studentId, bool active,
        CancellationToken cancellationToken = default)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        if (student is null)
        {
            return new StudentAdminOutcome(false, "That student no longer exists.");
        }

        if (active)
        {
            student.Reactivate();
        }
        else
        {
            student.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Deactivating rather than deleting: the attendance already captured
        // against this student is part of the course record, and removing the
        // row would cascade it away.
        return new StudentAdminOutcome(true,
            active
                ? $"{student.DisplayName} can sign in again."
                : $"{student.DisplayName} can no longer sign in. Their attendance record is kept.",
            student.Id);
    }

    public async Task<StudentAdminOutcome> ResetPasswordAsync(int studentId, string password,
        CancellationToken cancellationToken = default)
    {
        if (password.Length < MinimumPasswordLength)
        {
            return new StudentAdminOutcome(false, $"The new password needs at least {MinimumPasswordLength} characters.");
        }

        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        if (student is null)
        {
            return new StudentAdminOutcome(false, "That student no longer exists.");
        }

        student.SetPasswordHash(_hasher.Hash(password));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StudentAdminOutcome(true, "Password reset. Give the student the new one directly.", student.Id);
    }
}
