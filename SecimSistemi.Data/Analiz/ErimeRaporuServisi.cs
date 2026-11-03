using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Data.Analiz;

/// <summary>
/// Erime raporunu hazırlar: MahalleKarnesi görünümünü okur, hesabı <see cref="ErimeHesabi"/>'na bırakır.
/// Importer (rapor erime) ve Web (Sprint 4) aynı servisi kullanır.
/// </summary>
public sealed class ErimeRaporuServisi(SecimDbContext db)
{
    public async Task<ErimeRaporu> HazirlaAsync(int ilceId, OyTuru oyTuru, CancellationToken ct = default)
    {
        // Kale grubu her seride belediye meclisine göre belirlendiği için meclis satırları da okunur.
        var degerler = await db.MahalleKarnesi.AsNoTracking()
            .Where(k => k.IlceId == ilceId && k.SecimTuru == SecimTuru.Yerel
                        && (k.OyTuru == oyTuru || k.OyTuru == OyTuru.BelediyeMeclisi))
            .Select(k => new KarneDegeri(k.BirimKod, k.BirimAdi, k.Yil, k.OyTuru, k.ToplamOy, k.DspOyu, k.PusuladaMi))
            .ToListAsync(ct);

        return ErimeHesabi.Hesapla(degerler, oyTuru, ErimeHesabi.Kaleler(degerler));
    }
}
