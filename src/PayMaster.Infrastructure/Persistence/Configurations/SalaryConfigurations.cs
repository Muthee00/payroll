using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaster.Domain.Entities.Salary;

namespace PayMaster.Infrastructure.Persistence.Configurations;

public class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> b)
    {
        b.Property(c => c.Code).HasMaxLength(30);
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.Description).HasMaxLength(500);
        b.HasIndex(c => c.Code).IsUnique();
        b.Ignore(c => c.IsEarning);
    }
}

public class SalaryGradeConfiguration : IEntityTypeConfiguration<SalaryGrade>
{
    public void Configure(EntityTypeBuilder<SalaryGrade> b)
    {
        b.Property(g => g.Code).HasMaxLength(20);
        b.Property(g => g.Name).HasMaxLength(100);
        b.Property(g => g.Description).HasMaxLength(500);
        b.HasIndex(g => g.Code).IsUnique();
    }
}

public class SalaryStructureConfiguration : IEntityTypeConfiguration<SalaryStructure>
{
    public void Configure(EntityTypeBuilder<SalaryStructure> b)
    {
        b.Property(s => s.Code).HasMaxLength(30);
        b.Property(s => s.Name).HasMaxLength(150);
        b.Property(s => s.Description).HasMaxLength(500);
        b.HasIndex(s => s.Code).IsUnique();

        b.HasOne(s => s.SalaryGrade).WithMany().HasForeignKey(s => s.SalaryGradeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Department).WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Position).WithMany().HasForeignKey(s => s.PositionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalaryStructureComponentConfiguration : IEntityTypeConfiguration<SalaryStructureComponent>
{
    public void Configure(EntityTypeBuilder<SalaryStructureComponent> b)
    {
        b.Property(c => c.Amount).HasPrecision(18, 4);
        b.HasIndex(c => new { c.SalaryStructureId, c.SalaryComponentId }).IsUnique();
        b.HasOne(c => c.SalaryStructure).WithMany(s => s.Components)
            .HasForeignKey(c => c.SalaryStructureId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.SalaryComponent).WithMany()
            .HasForeignKey(c => c.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeSalaryConfiguration : IEntityTypeConfiguration<EmployeeSalary>
{
    public void Configure(EntityTypeBuilder<EmployeeSalary> b)
    {
        b.Property(s => s.ChangeReason).HasMaxLength(500);
        b.Property(s => s.RejectionReason).HasMaxLength(500);
        b.HasIndex(s => new { s.EmployeeId, s.Status, s.EffectiveFrom });

        b.HasOne(s => s.Employee).WithMany(e => e.SalaryHistory)
            .HasForeignKey(s => s.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.SalaryStructure).WithMany()
            .HasForeignKey(s => s.SalaryStructureId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(s => s.TotalRecurringEarnings);
        b.Ignore(s => s.GrossSalary);
    }
}

public class EmployeeSalaryComponentConfiguration : IEntityTypeConfiguration<EmployeeSalaryComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryComponent> b)
    {
        b.HasIndex(c => new { c.EmployeeSalaryId, c.SalaryComponentId }).IsUnique();
        b.HasOne(c => c.EmployeeSalary).WithMany(s => s.Components)
            .HasForeignKey(c => c.EmployeeSalaryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.SalaryComponent).WithMany()
            .HasForeignKey(c => c.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeDeductionConfiguration : IEntityTypeConfiguration<EmployeeDeduction>
{
    public void Configure(EntityTypeBuilder<EmployeeDeduction> b)
    {
        b.Property(d => d.Reference).HasMaxLength(100);
        b.HasIndex(d => new { d.EmployeeId, d.IsActive });
        b.HasOne(d => d.Employee).WithMany(e => e.RecurringDeductions)
            .HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.SalaryComponent).WithMany()
            .HasForeignKey(d => d.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(d => d.RemainingBalance);
    }
}
