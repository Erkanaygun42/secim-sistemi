using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecimSistemi.Domain;

namespace SecimSistemi.Data.Yapilandirma;

/// <summary>
/// <see cref="Sonuc"/> ve <see cref="OyOzeti"/> aynı bölge sütunlarını taşır; ortak kurallar burada.
/// </summary>
internal static class BolgeKaydiKurallari
{
    /// <summary>
    /// Seçilen düzey ve üstü dolu, altı boş olmalı. Örnek: Duzey = 'MahalleKoy' ise IlceId ve MahalleKoyId dolu, SandikId boş.
    /// </summary>
    public static string DuzeyTutarliligi =>
        """
        ("Duzey" = 'Il'         AND "IlceId" IS NULL     AND "MahalleKoyId" IS NULL     AND "SandikId" IS NULL) OR
        ("Duzey" = 'Ilce'       AND "IlceId" IS NOT NULL AND "MahalleKoyId" IS NULL     AND "SandikId" IS NULL) OR
        ("Duzey" = 'MahalleKoy' AND "IlceId" IS NOT NULL AND "MahalleKoyId" IS NOT NULL AND "SandikId" IS NULL) OR
        ("Duzey" = 'Sandik'     AND "IlceId" IS NOT NULL AND "MahalleKoyId" IS NOT NULL AND "SandikId" IS NOT NULL)
        """;
}

internal sealed class SonucYapilandirmasi : IEntityTypeConfiguration<Sonuc>
{
    public void Configure(EntityTypeBuilder<Sonuc> b)
    {
        // Aynı dosya iki kez yüklenince çift kayıt oluşmaz. Boş sütunlar (örnek ilçe düzeyinde SandikId)
        // birbirine eşit sayılır; PostgreSQL varsayılanında NULL'lar farklı sayıldığı için bu ayar şart.
        b.HasIndex(x => new { x.SecimId, x.OyTuru, x.IlId, x.IlceId, x.MahalleKoyId, x.SandikId, x.PartiId, x.AdayId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_Sonuclar_Tekil");

        // Raporlar için: bir seçimde bir partinin bütün oyları.
        b.HasIndex(x => new { x.PartiId, x.SecimId, x.OyTuru });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Sonuclar_PartiYaDaAday", "(\"PartiId\" IS NULL) <> (\"AdayId\" IS NULL)");
            t.HasCheckConstraint("CK_Sonuclar_Oy", "\"Oy\" >= 0");
            t.HasCheckConstraint("CK_Sonuclar_Duzey", BolgeKaydiKurallari.DuzeyTutarliligi);
        });
    }
}

internal sealed class OyOzetiYapilandirmasi : IEntityTypeConfiguration<OyOzeti>
{
    public void Configure(EntityTypeBuilder<OyOzeti> b)
    {
        b.HasIndex(x => new { x.SecimId, x.OyTuru, x.IlId, x.IlceId, x.MahalleKoyId, x.SandikId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_OyOzetleri_Tekil");

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OyOzetleri_Negatif",
                "COALESCE(\"KayitliSecmen\", 0) >= 0 AND COALESCE(\"OyKullanan\", 0) >= 0 AND COALESCE(\"GecerliOy\", 0) >= 0 AND COALESCE(\"GecersizOy\", 0) >= 0");
            t.HasCheckConstraint("CK_OyOzetleri_Duzey", BolgeKaydiKurallari.DuzeyTutarliligi);
        });
    }
}
