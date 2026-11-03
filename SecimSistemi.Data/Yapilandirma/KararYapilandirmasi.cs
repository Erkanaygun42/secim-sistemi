using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecimSistemi.Domain;

namespace SecimSistemi.Data.Yapilandirma;

internal sealed class MahalleDurumKarariYapilandirmasi : IEntityTypeConfiguration<MahalleDurumKarari>
{
    /// <summary>Geçerli karar: onaylanmış ve yerine başkası geçmemiş.</summary>
    private const string Gecerli = "\"Durum\" = 'Onaylandi' AND \"YerineGecenKararId\" IS NULL";

    public void Configure(EntityTypeBuilder<MahalleDurumKarari> b)
    {
        b.HasOne(x => x.MahalleKoy).WithMany().HasForeignKey(x => x.MahalleKoyId);
        b.HasOne(x => x.IliskiliMahalleKoy).WithMany().HasForeignKey(x => x.IliskiliMahalleKoyId);
        b.HasOne(x => x.Secim).WithMany().HasForeignKey(x => x.SecimId);
        b.HasOne(x => x.YerineGecenKarar).WithMany().HasForeignKey(x => x.YerineGecenKararId);
        b.Ignore(x => x.GecerliMi);

        b.Property(x => x.Gerekce).HasMaxLength(2000);
        b.Property(x => x.Kaynak).HasMaxLength(500);
        b.Property(x => x.TalepEden).HasMaxLength(100);
        b.Property(x => x.Onaylayan).HasMaxLength(100);
        b.Property(x => x.KararNotu).HasMaxLength(1000);

        // Bir konuda en fazla bir geçerli karar olur. Eşlemede ters yön (B, A) uygulamada denetlenir.
        b.HasIndex(x => new { x.MahalleKoyId, x.SecimId })
            .IsUnique()
            .HasFilter($"\"Karar\" IN ('Sabit', 'SiniriDegisti') AND {Gecerli}")
            .HasDatabaseName("IX_MahalleDurumKararlari_GecerliDurum");
        b.HasIndex(x => new { x.MahalleKoyId, x.IliskiliMahalleKoyId })
            .IsUnique()
            .HasFilter($"\"Karar\" = 'Esleme' AND {Gecerli}")
            .HasDatabaseName("IX_MahalleDurumKararlari_GecerliEsleme");
        b.HasIndex(x => x.Durum);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MahalleDurumKararlari_Tur",
                "(\"Karar\" IN ('Sabit', 'SiniriDegisti') AND \"SecimId\" IS NOT NULL AND \"IliskiliMahalleKoyId\" IS NULL AND \"EslemeTuru\" IS NULL) OR " +
                "(\"Karar\" = 'Esleme' AND \"IliskiliMahalleKoyId\" IS NOT NULL AND \"EslemeTuru\" IS NOT NULL AND \"IliskiliMahalleKoyId\" <> \"MahalleKoyId\")");
            t.HasCheckConstraint("CK_MahalleDurumKararlari_Onay",
                "(\"Durum\" = 'Talep' AND \"Onaylayan\" IS NULL AND \"KararZamani\" IS NULL AND \"YerineGecenKararId\" IS NULL) OR " +
                "(\"Durum\" = 'Reddedildi' AND \"Onaylayan\" IS NOT NULL AND \"KararZamani\" IS NOT NULL AND \"YerineGecenKararId\" IS NULL) OR " +
                "(\"Durum\" = 'Onaylandi' AND \"Onaylayan\" IS NOT NULL AND \"KararZamani\" IS NOT NULL)");
        });
    }
}
