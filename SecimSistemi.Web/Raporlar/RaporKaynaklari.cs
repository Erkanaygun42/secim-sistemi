using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Domain;

namespace SecimSistemi.Web.Raporlar;

/// <summary>Raporun dayandığı yüklenmiş YSK dosyaları (yükleme kaydından): "dosya adı (yüklendi 3 Kasım 2026)".</summary>
public sealed class RaporKaynaklari(IDbContextFactory<SecimDbContext> fabrika)
{
    /// <summary>
    /// <paramref name="secimId"/> verilirse o seçimin <paramref name="oyTuru"/> dosyası ile sınır analizinin kullandığı
    /// belediye meclisi dosyaları; verilmezse ilçenin bütün yerel seçim dosyaları.
    /// </summary>
    public async Task<KaynakListesi> ListeleAsync(int ilceId, int? secimId = null, OyTuru? oyTuru = null, CancellationToken ct = default)
    {
        await using var db = await fabrika.CreateDbContextAsync(ct);
        var sorgu = db.Yuklemeler.AsNoTracking().Where(y => y.IlceId == ilceId && y.Secim.Tur == SecimTuru.Yerel);
        if (secimId is not null)
            sorgu = sorgu.Where(y => (y.SecimId == secimId && y.OyTuru == oyTuru) || y.OyTuru == OyTuru.BelediyeMeclisi);

        var kayitlar = await sorgu.Select(y => new { y.DosyaAdi, y.YuklenmeZamani }).ToListAsync(ct);
        // Aynı dosya birden çok kez yüklendiyse en son yükleme gösterilir.
        var dosyalar = kayitlar
            .GroupBy(y => y.DosyaAdi)
            .Select(g => (Ad: g.Key, Zaman: g.Max(y => y.YuklenmeZamani)))
            .OrderBy(y => y.Ad, StringComparer.Ordinal)
            .ToList();
        return new KaynakListesi(
            dosyalar.Select(y => $"{y.Ad} (yüklendi {Metinler.TurkiyeSaati(y.Zaman).ToString("d MMMM yyyy", Metinler.Turkce)})").ToList(),
            dosyalar.Count == 0 ? null : Metinler.TurkiyeSaati(dosyalar.Max(y => y.Zaman)));
    }
}
