using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Organization;

namespace PayMaster.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(d => d.Code).HasMaxLength(20);
        b.Property(d => d.Name).HasMaxLength(100);
        b.Property(d => d.Description).HasMaxLength(500);
        b.HasIndex(d => d.Code).IsUnique();
        b.HasIndex(d => d.Name).IsUnique();

        b.HasOne(d => d.HeadEmployee).WithMany()
            .HasForeignKey(d => d.HeadEmployeeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.Property(p => p.Code).HasMaxLength(20);
        b.Property(p => p.Title).HasMaxLength(100);
        b.Property(p => p.Description).HasMaxLength(500);
        b.HasIndex(p => p.Code).IsUnique();

        b.HasOne(p => p.Department).WithMany(d => d.Positions)
            .HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.SalaryGrade).WithMany()
            .HasForeignKey(p => p.SalaryGradeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.Property(e => e.EmployeeNumber).HasMaxLength(20);
        b.Property(e => e.FirstName).HasMaxLength(100);
        b.Property(e => e.MiddleName).HasMaxLength(100);
        b.Property(e => e.LastName).HasMaxLength(100);
        b.Property(e => e.NationalId).HasMaxLength(20);
        b.Property(e => e.KraPin).HasMaxLength(11);
        b.Property(e => e.NssfNumber).HasMaxLength(20);
        b.Property(e => e.ShaNumber).HasMaxLength(30);
        b.Property(e => e.PersonalEmail).HasMaxLength(256);
        b.Property(e => e.WorkEmail).HasMaxLength(256);
        b.Property(e => e.PhoneNumber).HasMaxLength(20);
        b.Property(e => e.PhysicalAddress).HasMaxLength(500);
        b.Property(e => e.EmergencyContactName).HasMaxLength(150);
        b.Property(e => e.EmergencyContactRelationship).HasMaxLength(50);
        b.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
        b.Property(e => e.TerminationReason).HasMaxLength(500);
        b.Property(e => e.WorkLocation).HasMaxLength(100);
        b.Property(e => e.BankName).HasMaxLength(100);
        b.Property(e => e.BankBranch).HasMaxLength(100);
        b.Property(e => e.BankAccountNumber).HasMaxLength(34);
        b.Property(e => e.MobileMoneyNumber).HasMaxLength(20);
        b.Property(e => e.DisabilityExemptionCertificateNumber).HasMaxLength(50);

        b.HasIndex(e => e.EmployeeNumber).IsUnique();
        b.HasIndex(e => e.NationalId).IsUnique();
        b.HasIndex(e => e.KraPin).IsUnique();
        b.HasIndex(e => e.UserId).IsUnique();
        b.HasIndex(e => e.Status);
        b.HasIndex(e => new { e.LastName, e.FirstName });

        b.HasOne(e => e.Department).WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Position).WithMany()
            .HasForeignKey(e => e.PositionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Manager).WithMany(m => m.DirectReports)
            .HasForeignKey(e => e.ManagerId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(e => e.FullName);
        b.Ignore(e => e.Initials);
        b.Ignore(e => e.IsPayable);
    }
}

public class EmploymentHistoryConfiguration : IEntityTypeConfiguration<EmploymentHistory>
{
    public void Configure(EntityTypeBuilder<EmploymentHistory> b)
    {
        b.Property(h => h.FromDepartment).HasMaxLength(100);
        b.Property(h => h.ToDepartment).HasMaxLength(100);
        b.Property(h => h.FromPosition).HasMaxLength(100);
        b.Property(h => h.ToPosition).HasMaxLength(100);
        b.Property(h => h.Notes).HasMaxLength(1000);
        b.HasIndex(h => new { h.EmployeeId, h.EffectiveDate });
        b.HasOne(h => h.Employee).WithMany(e => e.EmploymentHistory)
            .HasForeignKey(h => h.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        b.Property(d => d.Title).HasMaxLength(200);
        b.Property(d => d.OriginalFileName).HasMaxLength(255);
        b.Property(d => d.StoragePath).HasMaxLength(500);
        b.Property(d => d.ContentType).HasMaxLength(100);
        b.HasOne(d => d.Employee).WithMany(e => e.Documents)
            .HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
