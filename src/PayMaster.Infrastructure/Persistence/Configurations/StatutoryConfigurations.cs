using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaster.Domain.Entities.Statutory;

namespace PayMaster.Infrastructure.Persistence.Configurations;

public class StatutoryConfigurationConfiguration : IEntityTypeConfiguration<StatutoryConfiguration>
{
    public void Configure(EntityTypeBuilder<StatutoryConfiguration> b)
    {
        b.Property(c => c.Name).HasMaxLength(150);
        b.Property(c => c.Description).HasMaxLength(1000);
        b.Property(c => c.LegalReference).HasMaxLength(2000);
        b.HasIndex(c => c.Version).IsUnique();
        b.HasIndex(c => new { c.Status, c.EffectiveFrom });
        b.Ignore(c => c.IsEditable);
    }
}

public class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> b)
    {
        b.Property(t => t.Rate).HasPrecision(9, 6);
        b.HasIndex(t => new { t.StatutoryConfigurationId, t.Order }).IsUnique();
        b.HasOne(t => t.StatutoryConfiguration).WithMany(c => c.TaxBrackets)
            .HasForeignKey(t => t.StatutoryConfigurationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StatutoryRateConfiguration : IEntityTypeConfiguration<StatutoryRate>
{
    public void Configure(EntityTypeBuilder<StatutoryRate> b)
    {
        b.Property(r => r.Value).HasPrecision(18, 6);
        b.Property(r => r.Notes).HasMaxLength(500);
        b.HasIndex(r => new { r.StatutoryConfigurationId, r.Key }).IsUnique();
        b.HasOne(r => r.StatutoryConfiguration).WithMany(c => c.Rates)
            .HasForeignKey(r => r.StatutoryConfigurationId).OnDelete(DeleteBehavior.Cascade);
    }
}
