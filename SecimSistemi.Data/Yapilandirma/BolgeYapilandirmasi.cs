using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecimSistemi.Domain;

namespace SecimSistemi.Data.Yapilandirma;

internal sealed class IlYapilandirmasi : IEntityTypeConfiguration<Il>
{
    public void Configure(EntityTypeBuilder<Il> b)
    {
        b.Property(x => x.Ad).HasMaxLength(50);
        b.HasIndex(x => x.Kod).IsUnique();
    }
}

internal sealed class IlceYapilandirmasi : IEntityTypeConfiguration<Ilce>
{
    public void Configure(EntityTypeBuilder<Ilce> b)
    {
        b.Property(x => x.Kod).HasMaxLength(20);
        b.Property(x => x.Ad).HasMaxLength(50);
        b.HasIndex(x => x.Kod).IsUnique();
        b.HasIndex(x => new { x.IlId, x.Ad });
    }
}

internal sealed class MahalleKoyYapilandirmasi : IEntityTypeConfiguration<MahalleKoy>
{
    public void Configure(EntityTypeBuilder<MahalleKoy> b)
    {
        b.Property(x => x.Ad).HasMaxLength(100);
        // YSK "Muhtarlık Id" ülke genelinde tekil sayılıyor (Sprint 1'de doğrulanacak).
        b.HasIndex(x => x.Kod).IsUnique();
        b.HasIndex(x => new { x.IlceId, x.Ad });
    }
}

internal sealed class SandikYapilandirmasi : IEntityTypeConfiguration<Sandik>
{
    public void Configure(EntityTypeBuilder<Sandik> b)
    {
        // Aynı dosya ikinci kez yüklenince aynı sandık yeniden oluşmaz.
        b.HasIndex(x => new { x.SecimId, x.MahalleKoyId, x.No }).IsUnique();
    }
}

internal sealed class IlceEslemesiYapilandirmasi : IEntityTypeConfiguration<IlceEslemesi>
{
    public void Configure(EntityTypeBuilder<IlceEslemesi> b)
    {
        b.HasOne(x => x.EskiIlce).WithMany().HasForeignKey(x => x.EskiIlceId);
        b.HasOne(x => x.YeniIlce).WithMany().HasForeignKey(x => x.YeniIlceId);
        b.Property(x => x.Not).HasMaxLength(500);
        b.HasIndex(x => new { x.EskiIlceId, x.YeniIlceId }).IsUnique();
    }
}
