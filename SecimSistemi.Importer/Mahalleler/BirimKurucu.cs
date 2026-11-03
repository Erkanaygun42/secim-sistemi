using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Importer.Mahalleler;

/// <summary>
/// Bir ilçenin karşılaştırma birimlerini veritabanındaki mahallelerden ve onaylı eşlemelerden (MahalleKoyEslemeleri)
/// baştan kurar. Birim kodu birimdeki en küçük Muhtarlık Id olduğu için yeniden kurulunca kodlar değişmez.
/// Çağıranın açık transaction'ı içinde çalışır; değişiklik izleyicisinde bekleyen kayıt olmamalıdır.
/// </summary>
public static class BirimKurucu
{
    public static async Task<IReadOnlyList<OlusanBirim>> YenidenKurAsync(SecimDbContext db, int ilceId, CancellationToken ct = default)
    {
        var mahalleler = await db.MahalleKoyler.AsNoTracking()
            .Where(m => m.IlceId == ilceId)
            .Select(m => new { m.Id, m.Kod, m.Ad })
            .ToListAsync(ct);
        var kenarlar = await db.MahalleKoyEslemeleri.AsNoTracking()
            .Where(e => e.EskiMahalleKoy.IlceId == ilceId || e.YeniMahalleKoy.IlceId == ilceId)
            .Select(e => new BirimKenari(e.EskiMahalleKoy.Kod, e.YeniMahalleKoy.Kod))
            .ToListAsync(ct);

        var birimler = BirimOlusturucu.Olustur(mahalleler.Select(m => new BirimMahallesi(m.Kod, m.Ad)).ToList(), kenarlar);

        await db.KarsilastirmaBirimiUyeleri.Where(u => u.Birim.IlceId == ilceId).ExecuteDeleteAsync(ct);
        await db.KarsilastirmaBirimleri.Where(b => b.IlceId == ilceId).ExecuteDeleteAsync(ct);

        var idler = mahalleler.ToDictionary(m => m.Kod, m => m.Id);
        foreach (var b in birimler)
        {
            db.KarsilastirmaBirimleri.Add(new KarsilastirmaBirimi
            {
                IlceId = ilceId,
                Kod = b.Kod,
                Ad = b.Ad,
                Uyeler = b.MahalleKodlari.Select(k => new KarsilastirmaBirimiUyesi { MahalleKoyId = idler[k] }).ToList(),
            });
        }
        await db.SaveChangesAsync(ct);
        return birimler;
    }
}
