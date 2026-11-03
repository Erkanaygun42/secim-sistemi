using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecimSistemi.Domain;

namespace SecimSistemi.Data.Yapilandirma;

internal sealed class SecimYapilandirmasi : IEntityTypeConfiguration<Secim>
{
    public void Configure(EntityTypeBuilder<Secim> b)
    {
        b.Property(x => x.Ad).HasMaxLength(100);
        // Aynı gün yapılan bütün oy türleri tek seçimdir; cumhurbaşkanlığı ikinci turu ayrı tarihtedir.
        b.HasIndex(x => x.Tarih).IsUnique();
    }
}

internal sealed class PartiYapilandirmasi : IEntityTypeConfiguration<Parti>
{
    public void Configure(EntityTypeBuilder<Parti> b)
    {
        b.Property(x => x.KisaAd).HasMaxLength(50);
        b.Property(x => x.TamAd).HasMaxLength(200);
        b.HasIndex(x => x.KisaAd).IsUnique();
    }
}

internal sealed class PartiAdEslemesiYapilandirmasi : IEntityTypeConfiguration<PartiAdEslemesi>
{
    public void Configure(EntityTypeBuilder<PartiAdEslemesi> b)
    {
        b.Property(x => x.HamAd).HasMaxLength(100);
        // Boş başlangıç tarihi de bir değer sayılır: aynı ham ad için iki "sınırsız" kayıt olamaz.
        b.HasIndex(x => new { x.HamAd, x.GecerliBaslangic }).IsUnique().AreNullsDistinct(false);
        b.ToTable(t => t.HasCheckConstraint("CK_PartiAdEslemeleri_TarihAraligi",
            "\"GecerliBaslangic\" IS NULL OR \"GecerliBitis\" IS NULL OR \"GecerliBaslangic\" <= \"GecerliBitis\""));
    }
}

internal sealed class AdayYapilandirmasi : IEntityTypeConfiguration<Aday>
{
    public void Configure(EntityTypeBuilder<Aday> b)
    {
        b.Property(x => x.AdSoyad).HasMaxLength(150);
        b.Ignore(x => x.BagimsizMi);
        // Büyükşehir adayında ilçe boştur; boş ilçe de karşılaştırmaya girer.
        b.HasIndex(x => new { x.SecimId, x.OyTuru, x.IlId, x.IlceId, x.AdSoyad }).IsUnique().AreNullsDistinct(false);
    }
}

internal sealed class DspKatilimiYapilandirmasi : IEntityTypeConfiguration<DspKatilimi>
{
    public void Configure(EntityTypeBuilder<DspKatilimi> b)
    {
        b.HasOne(x => x.ListePartisi).WithMany().HasForeignKey(x => x.ListePartiId);
        b.Property(x => x.IttifakAdi).HasMaxLength(100);
        b.Property(x => x.Not).HasMaxLength(500);
        b.HasIndex(x => new { x.SecimId, x.OyTuru, x.IlId, x.IlceId }).IsUnique().AreNullsDistinct(false);
        b.ToTable(t => t.HasCheckConstraint("CK_DspKatilimlari_ListePartisi",
            "(\"Durum\" = 'BaskaPartininListesi') = (\"ListePartiId\" IS NOT NULL)"));
    }
}
