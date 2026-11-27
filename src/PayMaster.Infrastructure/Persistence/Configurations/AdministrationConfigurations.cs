using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaster.Domain.Entities.Administration;

namespace PayMaster.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(n => n.Title).HasMaxLength(200);
        b.Property(n => n.Message).HasMaxLength(1000);
        b.Property(n => n.Link).HasMaxLength(500);
        b.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(a => a.UserName).HasMaxLength(256);
        b.Property(a => a.Action).HasMaxLength(100);
        b.Property(a => a.EntityName).HasMaxLength(100);
        b.Property(a => a.EntityId).HasMaxLength(64);
        b.Property(a => a.OldValues).HasColumnType("longtext");
        b.Property(a => a.NewValues).HasColumnType("longtext");
        b.Property(a => a.Details).HasMaxLength(2000);
        b.Property(a => a.IpAddress).HasMaxLength(64);
        b.HasIndex(a => new { a.EntityName, a.EntityId });
        b.HasIndex(a => a.Timestamp);
        b.HasIndex(a => a.UserId);
    }
}

public class CompanySettingConfiguration : IEntityTypeConfiguration<CompanySetting>
{
    public void Configure(EntityTypeBuilder<CompanySetting> b)
    {
        b.Property(c => c.CompanyName).HasMaxLength(200);
        b.Property(c => c.LegalName).HasMaxLength(200);
        b.Property(c => c.KraPin).HasMaxLength(11);
        b.Property(c => c.NssfEmployerNumber).HasMaxLength(30);
        b.Property(c => c.ShaEmployerNumber).HasMaxLength(30);
        b.Property(c => c.PhysicalAddress).HasMaxLength(500);
        b.Property(c => c.PostalAddress).HasMaxLength(200);
        b.Property(c => c.PhoneNumber).HasMaxLength(30);
        b.Property(c => c.Email).HasMaxLength(256);
        b.Property(c => c.Website).HasMaxLength(256);
        b.Property(c => c.LogoPath).HasMaxLength(500);
        b.Property(c => c.EmployeeNumberPrefix).HasMaxLength(10);
        b.Property(c => c.PayslipFooter).HasMaxLength(1000);
        b.Property(c => c.MinimumNetPayRatio).HasPrecision(9, 6);
    }
}
