using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VanEscolar.Api.Domain;
namespace VanEscolar.Api.Data;
public sealed class VanDb(DbContextOptions<VanDb> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options) {
  public DbSet<Tenant> Tenants => Set<Tenant>();
  public DbSet<Membership> Memberships => Set<Membership>();
  public DbSet<School> Schools => Set<School>();
  public DbSet<Student> Students => Set<Student>();
  public DbSet<Guardian> Guardians => Set<Guardian>();
  public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
  public DbSet<TransportContract> Contracts => Set<TransportContract>();
  public DbSet<Charge> Charges => Set<Charge>();
  public DbSet<Payment> Payments => Set<Payment>();
  public DbSet<Expense> Expenses => Set<Expense>();
  public DbSet<AbsenceNotice> Absences => Set<AbsenceNotice>();
  public DbSet<AuditEvent> Audits => Set<AuditEvent>();
  protected override void OnModelCreating(ModelBuilder b) {
    base.OnModelCreating(b);
    b.Entity<Membership>().HasIndex(x => new {x.TenantId, x.UserId}).IsUnique();
    b.Entity<School>().HasIndex(x => new {x.TenantId, x.Name}).IsUnique();
    b.Entity<Student>().HasIndex(x => new {x.TenantId, x.FullName});
    b.Entity<StudentGuardian>().HasIndex(x => new {x.TenantId, x.StudentId, x.GuardianId}).IsUnique();
    b.Entity<TransportContract>().Property(x => x.MonthlyAmount).HasPrecision(18, 2);
    b.Entity<TransportContract>().HasIndex(x => new {x.TenantId, x.StudentId}).IsUnique().HasFilter("\"Status\" = 'Active'");
    b.Entity<Charge>().Property(x => x.Amount).HasPrecision(18, 2);
    b.Entity<Charge>().HasIndex(x => new {x.TenantId, x.ContractId, x.Competence}).IsUnique();
    b.Entity<Charge>().HasIndex(x => new {x.TenantId, x.Status, x.DueDate});
    b.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);
    b.Entity<Payment>().HasIndex(x => new {x.TenantId, x.IdempotencyKey}).IsUnique();
    b.Entity<Payment>().HasIndex(x => new {x.TenantId, x.ChargeId, x.PaidAtUtc});
    b.Entity<Expense>().Property(x => x.Amount).HasPrecision(18, 2);
    b.Entity<Expense>().HasIndex(x => new {x.TenantId, x.Date});
    b.Entity<AbsenceNotice>().HasIndex(x => new {x.TenantId, x.Date});
    b.Entity<AuditEvent>().HasIndex(x => new {x.TenantId, x.OccurredAtUtc});
    b.Entity<Student>().HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<Guardian>().HasIndex(x => new {x.TenantId, x.Phone});
    b.Entity<StudentGuardian>().HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<StudentGuardian>().HasOne<Guardian>().WithMany().HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<TransportContract>().HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<Charge>().HasOne<TransportContract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<Payment>().HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
    b.Entity<AbsenceNotice>().HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
  }
}
