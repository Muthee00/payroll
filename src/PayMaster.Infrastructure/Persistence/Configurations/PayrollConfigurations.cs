using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaster.Domain.Entities.Payroll;

namespace PayMaster.Infrastructure.Persistence.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> b)
    {
        b.Property(p => p.Name).HasMaxLength(50);
        b.HasIndex(p => new { p.Year, p.Month }).IsUnique();
        b.Ignore(p => p.CalendarDays);
    }
}

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> b)
    {
        b.Property(r => r.Reference).HasMaxLength(40);
        b.Property(r => r.StatusReason).HasMaxLength(1000);
        b.Property(r => r.Notes).HasMaxLength(1000);
        b.Property(r => r.StatutorySnapshotJson).HasColumnType("longtext");
        b.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();

        b.HasIndex(r => r.Reference).IsUnique();
        b.HasIndex(r => new { r.PayrollPeriodId, r.Status });

        b.HasOne(r => r.PayrollPeriod).WithMany(p => p.Runs)
            .HasForeignKey(r => r.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.OriginalRun).WithMany()
            .HasForeignKey(r => r.OriginalRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Department).WithMany()
            .HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.StatutoryConfiguration).WithMany()
            .HasForeignKey(r => r.StatutoryConfigurationId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(r => r.IsEditable);
        b.Ignore(r => r.IsCalculated);
        b.Ignore(r => r.IsLockedForHistory);
        b.Ignore(r => r.HoldsPeriodLock);
    }
}

public class PayrollEmployeeConfiguration : IEntityTypeConfiguration<PayrollEmployee>
{
    public void Configure(EntityTypeBuilder<PayrollEmployee> b)
    {
        b.Property(p => p.EmployeeNumber).HasMaxLength(20);
        b.Property(p => p.EmployeeName).HasMaxLength(300);
        b.Property(p => p.DepartmentName).HasMaxLength(100);
        b.Property(p => p.JobTitle).HasMaxLength(100);
        b.Property(p => p.KraPin).HasMaxLength(11);
        b.Property(p => p.NssfNumber).HasMaxLength(20);
        b.Property(p => p.ShaNumber).HasMaxLength(30);
        b.Property(p => p.NationalId).HasMaxLength(20);
        b.Property(p => p.BankName).HasMaxLength(100);
        b.Property(p => p.BankBranch).HasMaxLength(100);
        b.Property(p => p.BankAccountNumber).HasMaxLength(34);
        b.Property(p => p.MobileMoneyNumber).HasMaxLength(20);
        b.Property(p => p.Errors).HasMaxLength(2000);
        b.Property(p => p.Warnings).HasMaxLength(2000);
        b.Property(p => p.CalculationBreakdownJson).HasColumnType("longtext");

        // Duplicate-payroll guard: an employee can be held by only one live run per period.
        b.HasIndex(p => new { p.PayrollPeriodId, p.EmployeeId, p.PeriodLock }).IsUnique();
        b.HasIndex(p => new { p.PayrollRunId, p.EmployeeId }).IsUnique();
        b.HasIndex(p => p.EmployeeId);

        b.HasOne(p => p.PayrollRun).WithMany(r => r.Employees)
            .HasForeignKey(p => p.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PayrollPeriod>().WithMany()
            .HasForeignKey(p => p.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Employee).WithMany()
            .HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayrollEarningConfiguration : IEntityTypeConfiguration<PayrollEarning>
{
    public void Configure(EntityTypeBuilder<PayrollEarning> b)
    {
        b.Property(e => e.Code).HasMaxLength(30);
        b.Property(e => e.Name).HasMaxLength(100);
        b.HasOne(e => e.PayrollEmployee).WithMany(p => p.Earnings)
            .HasForeignKey(e => e.PayrollEmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayrollDeductionConfiguration : IEntityTypeConfiguration<PayrollDeduction>
{
    public void Configure(EntityTypeBuilder<PayrollDeduction> b)
    {
        b.Property(d => d.Code).HasMaxLength(30);
        b.Property(d => d.Name).HasMaxLength(100);
        b.HasIndex(d => d.EmployeeDeductionId);
        b.HasOne(d => d.PayrollEmployee).WithMany(p => p.Deductions)
            .HasForeignKey(d => d.PayrollEmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayrollEmployerContributionConfiguration : IEntityTypeConfiguration<PayrollEmployerContribution>
{
    public void Configure(EntityTypeBuilder<PayrollEmployerContribution> b)
    {
        b.Property(c => c.Code).HasMaxLength(30);
        b.Property(c => c.Name).HasMaxLength(100);
        b.HasOne(c => c.PayrollEmployee).WithMany(p => p.EmployerContributions)
            .HasForeignKey(c => c.PayrollEmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayrollInputConfiguration : IEntityTypeConfiguration<PayrollInput>
{
    public void Configure(EntityTypeBuilder<PayrollInput> b)
    {
        b.Property(i => i.Quantity).HasPrecision(18, 4);
        b.Property(i => i.Rate).HasPrecision(18, 4);
        b.Property(i => i.Description).HasMaxLength(500);
        b.HasIndex(i => new { i.PayrollPeriodId, i.EmployeeId });
        b.HasOne(i => i.Employee).WithMany().HasForeignKey(i => i.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.PayrollPeriod).WithMany().HasForeignKey(i => i.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.SalaryComponent).WithMany().HasForeignKey(i => i.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> b)
    {
        b.Property(p => p.PayslipNumber).HasMaxLength(40);
        b.Property(p => p.EmailError).HasMaxLength(1000);
        b.HasIndex(p => p.PayslipNumber).IsUnique();
        b.HasIndex(p => p.PayrollEmployeeId).IsUnique();
        b.HasIndex(p => p.EmployeeId);
        b.HasOne(p => p.PayrollEmployee).WithOne(e => e.Payslip)
            .HasForeignKey<Payslip>(p => p.PayrollEmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
