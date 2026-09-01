using AttendanceRegister.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AttendanceRegister.Data;

public sealed class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Lecturer> Lecturers => Set<Lecturer>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceQuery> AttendanceQueries => Set<AttendanceQuery>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- Users: table-per-hierarchy ------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(400).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasDiscriminator<string>("UserType")
                  .HasValue<Student>("Student")
                  .HasValue<Lecturer>("Lecturer");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.Property(s => s.StudentNumber).HasMaxLength(20);
            entity.HasIndex(s => s.StudentNumber)
                  .IsUnique()
                  .HasFilter("\"StudentNumber\" IS NOT NULL");
        });

        modelBuilder.Entity<Lecturer>(entity =>
        {
            entity.Property(l => l.StaffNumber).HasMaxLength(20);
        });

        // ---- Course ---------------------------------------------------------
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Code).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Title).HasMaxLength(200).IsRequired();
            entity.HasIndex(c => c.Code).IsUnique();
        });

        // ---- Lecture --------------------------------------------------------
        modelBuilder.Entity<Lecture>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Topic).HasMaxLength(200);
            entity.Property(l => l.Venue).HasMaxLength(100);
            entity.Property(l => l.CheckInCode).HasMaxLength(12);

            entity.HasOne(l => l.Course)
                  .WithMany(c => c.Lectures)
                  .HasForeignKey(l => l.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);

            // One session per course per calendar date.
            entity.HasIndex(l => new { l.CourseId, l.SessionDate }).IsUnique();
        });

        // ---- AttendanceRecord ----------------------------------------------
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Note).HasMaxLength(500);

            entity.HasOne(a => a.Lecture)
                  .WithMany(l => l.AttendanceRecords)
                  .HasForeignKey(a => a.LectureId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Student)
                  .WithMany(s => s.AttendanceRecords)
                  .HasForeignKey(a => a.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // The database, not the application, is the last line of defence
            // against a duplicate record for the same student and lecture.
            entity.HasIndex(a => new { a.LectureId, a.StudentId }).IsUnique();
        });

        // ---- AttendanceQuery -------------------------------------------------
        modelBuilder.Entity<AttendanceQuery>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(q => q.ResolutionNote).HasMaxLength(1000);

            entity.HasOne(q => q.Student)
                  .WithMany(s => s.Queries)
                  .HasForeignKey(q => q.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(q => q.Lecture)
                  .WithMany()
                  .HasForeignKey(q => q.LectureId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(q => q.Status);
        });

        // ---- ImportBatch -----------------------------------------------------
        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.FileName).HasMaxLength(260).IsRequired();
        });
    }
}
