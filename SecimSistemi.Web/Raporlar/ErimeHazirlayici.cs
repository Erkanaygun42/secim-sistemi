using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>Erime raporunun adresteki filtreleri: <c>?ilce=34-ESENYURT&amp;oy-turu=belediye-meclisi</c>.</summary>
public sealed record ErimeSorgusu(string? Ilce, string? OyTuru)
{
    public static ErimeSorgusu Oku(IQueryCollection q) => new(q["ilce"], q["oy-turu"]);

    public Dictionary<string, object?> Parametreler() => new() { ["ilce"] = Ilce, ["oy-turu"] = OyTuru };

    public string SorguMetni() =>
        string.Join("&", Parametreler().Where(p => p.Value is not null).Select(p => $"{p.Key}={Uri.EscapeDataString((string)p.Value!)}"));
}

/// <param name="IlceErimesi">İlçenin tamamı için 2009'dan bugüne, her seri.</param>
/// <param name="Raporlar">Mahalle düzeyi erime, her seri (kale tablosu bütün serileri gösterir).</param>
/// <param name="OyTuru">Mahalle tablosunda gösterilen seri.</param>
public sealed record ErimeSonucu(
    IReadOnlyList<IlceSecenegi> Ilceler, IlceSecenegi? Ilce, IReadOnlyList<OyTuru> Seriler, OyTuru OyTuru,
    IReadOnlyDictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>> IlceErimesi,
    IReadOnlyDictionary<OyTuru, ErimeRaporu> Raporlar, SinirAnaliziSonucu? Sinir, ErimeSorgusu Sorgu)
{
    public ErimeRaporu? Rapor => Raporlar.GetValueOrDefault(OyTuru);

    public string FiltreOzeti => Ilce is null ? "" :
        $"{Ilce.IlAdi} / {Ilce.Ad} · yerel seçimler · taban {ErimeHesabi.TabanYili} · mahalle tablosu: {Metinler.Ad(OyTuru)}";

    /// <summary>Mahalle satırları dönemlerine göre: en uzun dönem önce; dönem içinde raporun sırası (kayıp oy).</summary>
    public IEnumerable<IGrouping<string, ErimeSatiri>> DonemGruplari() =>
        (Rapor?.Satirlar ?? []).GroupBy(s => Sinir?.Birim(s.BirimKod)?.Donem ?? Metinler.Bos)
            .OrderBy(g => Sinir?.Birim(g.First().BirimKod)?.DonemBaslangici ?? int.MaxValue);

    /// <summary>Satırın sınır uyarısı: dönem içinde şüpheli seçim varsa metni, yoksa boş.</summary>
    public string? Uyari(int birimKod) =>
        Sinir?.Birim(birimKod) is { SupheliVar: true } b ? Metinler.Uyari(b.SupheliYillar(b.DonemSonu)) : null;

    public string KaleOzeti
    {
        get
        {
            if (Rapor is null || Sinir is null) return "";
            var sabit = Sinir.Birimler.Count(b => b.DonemBaslangici == ErimeHesabi.TabanYili);
            var adlar = Rapor.KaleKodlari.Select(k => Sinir.Birim(k)?.BirimAdi ?? k.ToString(Metinler.Turkce));
            return $"2009'dan beri karşılaştırılabilir {sabit} birim içinde 2009 belediye meclisi oranına göre en güçlü " +
                   $"%{ErimeHesabi.KaleDilimi * 100:0} ({Rapor.KaleKodlari.Count} birim): {string.Join(", ", adlar)}";
        }
    }
}

/// <summary>
/// Erime raporunu hazırlar; Importer'daki <c>rapor erime</c> ile aynı servisleri kullanır (ErimeRaporuServisi, SinirAnaliziServisi).
/// Seriler: ilçede verisi olan yerel seçim oy türleri (meclisler ve başkanlıklar). Varsayılan seri belediye meclisi.
/// </summary>
public sealed class ErimeHazirlayici(IDbContextFactory<SecimDbContext> fabrika)
{
    private static readonly OyTuru[] YerelSeriler = [OyTuru.BelediyeMeclisi, OyTuru.IlGenelMeclisi, OyTuru.BelediyeBaskani, OyTuru.BuyuksehirBaskani];

    public async Task<ErimeSonucu> HazirlaAsync(ErimeSorgusu sorgu, CancellationToken ct = default)
    {
        await using var db = await fabrika.CreateDbContextAsync(ct);
        var karne = new KarneServisi(db);

        var ilceler = await karne.IlcelerAsync(ct);
        var ilce = ilceler.FirstOrDefault(i => i.Kod == sorgu.Ilce) ?? ilceler.FirstOrDefault();
        if (ilce is null)
            return new ErimeSonucu(ilceler, null, [], OyTuru.BelediyeMeclisi, new Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>>(),
                new Dictionary<OyTuru, ErimeRaporu>(), null, sorgu);

        var mevcut = await db.MahalleKarnesi.AsNoTracking()
            .Where(k => k.IlceId == ilce.Id && k.SecimTuru == SecimTuru.Yerel)
            .Select(k => k.OyTuru).Distinct().ToListAsync(ct);
        var seriler = YerelSeriler.Where(mevcut.Contains).ToList();
        var istenen = Metinler.OyTuruCoz(sorgu.OyTuru);
        var oyTuru = istenen is { } o && seriler.Contains(o) ? o : seriler.FirstOrDefault();

        var sinir = await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, KarneServisi.Esikler, ct: ct);
        var servis = new ErimeRaporuServisi(db);
        var ilceErimesi = new Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>>();
        var raporlar = new Dictionary<OyTuru, ErimeRaporu>();
        foreach (var seri in seriler)
        {
            ilceErimesi[seri] = await servis.IlceErimesiAsync(ilce.Id, seri, ct);
            raporlar[seri] = await servis.HazirlaAsync(ilce.Id, seri, sinir, ct);
        }

        var cozulmus = new ErimeSorgusu(ilce.Kod, seriler.Count == 0 ? null : Metinler.Kod(oyTuru));
        return new ErimeSonucu(ilceler, ilce, seriler, oyTuru, ilceErimesi, raporlar, sinir, cozulmus);
    }
}
