using AttendanceSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<ClassSession> Sessions => Set<ClassSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.UniversityId).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.UniversityId).HasMaxLength(32);
            e.Property(x => x.FullName).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(160);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Course>(e =>
        {
            e.ToTable("courses");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(160);
            e.HasOne(x => x.Professor).WithMany().HasForeignKey(x => x.ProfessorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ProfessorId);
        });

        b.Entity<Enrollment>(e =>
        {
            e.ToTable("enrollments");
            e.HasIndex(x => new { x.CourseId, x.StudentId }).IsUnique();
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Classroom>(e => { e.ToTable("classrooms"); e.Property(x => x.Name).HasMaxLength(120); });

        b.Entity<ClassSession>(e =>
        {
            e.ToTable("sessions");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CourseId);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Classroom).WithMany().HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AttendanceRecord>(e =>
        {
            e.ToTable("attendance_records");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            e.HasIndex(x => x.StudentId);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<NotificationLog>(e =>
        {
            e.ToTable("notification_logs");
            e.Property(x => x.Type).HasMaxLength(40);
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasIndex(x => new { x.StudentId, x.CourseId, x.Type });
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
        });
    }
}
