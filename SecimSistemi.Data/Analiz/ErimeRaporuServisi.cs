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
    /// <summary>
    /// Mahalle (birim) düzeyi erime. <paramref name="sinir"/> verilirse her birimin tabanı kendi karşılaştırılabilir
    /// döneminin ilk seçimidir ve kale grubu yalnızca 2009'dan beri karşılaştırılabilir birimlerden seçilir;
    /// verilmezse bütün birimler 2009 tabanını kullanır.
    /// </summary>
    public async Task<ErimeRaporu> HazirlaAsync(int ilceId, OyTuru oyTuru, SinirAnaliziSonucu? sinir = null, CancellationToken ct = default)
    {
        // Kale grubu her seride belediye meclisine göre belirlendiği için meclis satırları da okunur.
        var degerler = await KarneAsync(ilceId, oyTuru, OyTuru.BelediyeMeclisi, ct);

        IReadOnlyDictionary<int, int>? tabanYillari = sinir?.Birimler.ToDictionary(b => b.BirimKod, b => b.DonemBaslangici);
        IReadOnlySet<int>? kaleAdaylari = sinir?.Birimler.Where(b => b.DonemBaslangici == ErimeHesabi.TabanYili).Select(b => b.BirimKod).ToHashSet();

        return ErimeHesabi.Hesapla(degerler, oyTuru, ErimeHesabi.Kaleler(degerler, kaleAdaylari), tabanYillari);
    }

    /// <summary>İlçenin tamamı için 2009'dan bugüne erime (bir seri).</summary>
    public async Task<IReadOnlyList<IlceErimeSatiri>> IlceErimesiAsync(int ilceId, OyTuru oyTuru, CancellationToken ct = default) =>
        ErimeHesabi.IlceErimesi(await KarneAsync(ilceId, oyTuru, oyTuru, ct), oyTuru);

    private async Task<List<KarneDegeri>> KarneAsync(int ilceId, OyTuru oyTuru, OyTuru ikinciOyTuru, CancellationToken ct) =>
        await db.MahalleKarnesi.AsNoTracking()
            .Where(k => k.IlceId == ilceId && k.SecimTuru == SecimTuru.Yerel && (k.OyTuru == oyTuru || k.OyTuru == ikinciOyTuru))
            .Select(k => new KarneDegeri(k.BirimKod, k.BirimAdi, k.Yil, k.OyTuru, k.ToplamOy, k.DspOyu, k.PusuladaMi))
            .ToListAsync(ct);
}
