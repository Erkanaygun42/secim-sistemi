using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecimSistemi.Domain;

namespace SecimSistemi.Data.Yapilandirma;

internal sealed class KarsilastirmaBirimiYapilandirmasi : IEntityTypeConfiguration<KarsilastirmaBirimi>
{
    public void Configure(EntityTypeBuilder<KarsilastirmaBirimi> b)
    {
        b.Property(x => x.Ad).HasMaxLength(2000);
        // Kod = birimdeki en küçük Muhtarlık Id; ilçe içinde tekildir.
        b.HasIndex(x => new { x.IlceId, x.Kod }).IsUnique();
    }
}

internal sealed class KarsilastirmaBirimiUyesiYapilandirmasi : IEntityTypeConfiguration<KarsilastirmaBirimiUyesi>
{
    public void Configure(EntityTypeBuilder<KarsilastirmaBirimiUyesi> b)
    {
        // Birincil anahtar mahalle: her mahalle tam olarak bir birime ait olabilir.
        b.HasKey(x => x.MahalleKoyId);
        b.Property(x => x.MahalleKoyId).ValueGeneratedNever();
        b.HasOne(x => x.MahalleKoy).WithMany().HasForeignKey(x => x.MahalleKoyId);
        b.HasOne(x => x.Birim).WithMany(x => x.Uyeler).HasForeignKey(x => x.BirimId);
        b.HasIndex(x => x.BirimId);
    }
}
