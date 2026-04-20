using HeartCathAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace HeartCathAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<User> Users { get; set; }
    
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Study> Studies { get; set; }
    public DbSet<AnalysisResult> AnalysisResults { get; set; }

    //Auth
    public DbSet<PasswordResetOtp> PasswordResetOtps { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Studies)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Patients)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Patient>()
            .HasMany(p => p.Studies)
            .WithOne(s => s.Patient)
            .HasForeignKey(s => s.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Study>()
            .Property(s => s.FileType)
            .HasConversion<int>();

        modelBuilder.Entity<Study>()
            .HasMany(s => s.AnalysisResults)
            .WithOne(a => a.Study)
            .HasForeignKey(a => a.StudyId)
            .OnDelete(DeleteBehavior.Cascade);
        //Auth
        modelBuilder.Entity<User>()
               .Property(u => u.Email)
               .UseCollation("SQL_Latin1_General_CP1_CS_AS");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }
}