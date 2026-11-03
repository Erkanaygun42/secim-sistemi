using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecimSistemi.Domain;

namespace SecimSistemi.Data.Yapilandirma;

internal sealed class YuklemeYapilandirmasi : IEntityTypeConfiguration<Yukleme>
{
    public void Configure(EntityTypeBuilder<Yukleme> b)
    {
        b.Property(x => x.DosyaAdi).HasMaxLength(255);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.KontrolDosyasiSha256).HasMaxLength(64);

        // Aynı içerik iki kez yüklenmez.
        b.HasIndex(x => x.Sha256).IsUnique();

        // Bir seçim + oy türü + ilçe için yalnızca bir aktif yükleme olur; eskiler "Yenilendi" olarak kalır.
        b.HasIndex(x => new { x.SecimId, x.OyTuru, x.IlceId })
            .IsUnique()
            .HasFilter("\"Durum\" = 'Aktif'")
            .HasDatabaseName("IX_Yuklemeler_Aktif");

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Yuklemeler_Sha256", "\"Sha256\" ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("CK_Yuklemeler_Sayilar",
                "\"SatirSayisi\" >= 0 AND \"SandikSayisi\" >= 0 AND \"MahalleSayisi\" >= 0 AND \"ToplamOy\" >= 0 AND COALESCE(\"DspOyu\", 0) >= 0");
        });
    }
}
