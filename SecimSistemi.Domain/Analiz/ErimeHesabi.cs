namespace SecimSistemi.Domain.Analiz;

/// <summary>Bir karşılaştırma biriminin bir seçimdeki bir oy türü toplamları (MahalleKarnesi görünümünün bir satırı).</summary>
/// <param name="DspOyu">DSP pusulada değilse boş; pusuladaysa oy (0 olabilir).</param>
public sealed record KarneDegeri(int BirimKod, string BirimAdi, int Yil, OyTuru OyTuru, long ToplamOy, long? DspOyu, bool PusuladaMi);

/// <summary>
/// Bir birimin kendi tabanına göre erimesi. Taban, birimin karşılaştırılabilir döneminin ilk seçimidir
/// (2009'dan beri sabit birimde 2009). Oranlar yüzde (0–100), korunma oranı kat (1 = aynı kaldı).
/// </summary>
public sealed record ErimeSatiri(
    int BirimKod,
    string BirimAdi,
    int TabanYili,
    decimal? TabanOrani,
    long? TabanToplamOy,
    long? TabanDspOyu,
    decimal? SonOrani,
    long? SonToplamOy,
    long? SonDspOyu,
    decimal? PuanFarki,
    decimal? KorunmaOrani,
    decimal? BeklenenOy,
    decimal? KayipOy,
    bool KaleMi,
    string? Not);

/// <summary>2009 kaleleri grubunun bir seçimdeki ağırlıklı oranı: grubun DSP oyu ÷ grubun toplam oyu.</summary>
public sealed record KaleOrtalamasi(int Yil, int BirimSayisi, long ToplamOy, long? DspOyu, decimal? Oran);

public sealed record ErimeRaporu(
    OyTuru OyTuru,
    int TabanYili,
    int? SonYil,
    IReadOnlyList<ErimeSatiri> Satirlar,
    IReadOnlyList<int> KaleKodlari,
    IReadOnlyList<KaleOrtalamasi> KaleOrtalamalari);

/// <summary>İlçenin tamamı için bir seçimdeki değerler ve 2009'a göre erime.</summary>
public sealed record IlceErimeSatiri(int Yil, long ToplamOy, long? DspOyu, decimal? Oran, decimal? PuanFarki, decimal? KorunmaOrani, decimal? BeklenenOy, decimal? KayipOy);

/// <summary>
/// Erime raporunun hesap kuralları (veritabanından bağımsız):
/// <list type="bullet">
/// <item>Son seçim: o seride (oy türünde) DSP'nin pusulada olduğu en son yerel seçim.</item>
/// <item>Taban: birimin karşılaştırılabilir döneminin ilk seçimi; verilmezse 2009.</item>
/// <item>Korunma oranı = son oran ÷ taban oranı; taban oranı 0 ya da boşsa boş.</item>
/// <item>Beklenen oy = taban oranı × son seçimdeki toplam oy; kayıp oy = beklenen oy − gerçek oy.</item>
/// <item>DSP'nin pusulada olmadığı seçimde oran ve oy 0 değil boştur.</item>
/// <item>2009 kaleleri: 2009 belediye meclisi oranına göre en güçlü %20'lik birimler (yukarı yuvarlanır;
///   eşitlikte küçük kod önce). Grup sabittir, bütün serilerde aynıdır. Yalnızca 2009'dan beri karşılaştırılabilir
///   birimler aday olabilir (çağıran verir).</item>
/// </list>
/// </summary>
public static class ErimeHesabi
{
    public const int TabanYili = 2009;
    public const decimal KaleDilimi = 0.20m;

    /// <summary>Yüzde oran; DSP pusulada değilse ya da toplam oy 0 ise boş.</summary>
    public static decimal? Oran(long toplamOy, long? dspOyu, bool pusuladaMi) =>
        pusuladaMi && dspOyu is { } d && toplamOy > 0 ? 100m * d / toplamOy : null;

    private static decimal? Oran(KarneDegeri? k) => k is null ? null : Oran(k.ToplamOy, k.DspOyu, k.PusuladaMi);

    /// <summary>2009 belediye meclisi oranına göre kale grubunun birim kodları.</summary>
    /// <param name="adaylar">Verilirse yalnızca bu birimler kale olabilir (örnek 2009'dan beri sınırı değişmeyenler).</param>
    public static IReadOnlyList<int> Kaleler(IEnumerable<KarneDegeri> degerler, IReadOnlySet<int>? adaylar = null)
    {
        var sirali = degerler
            .Where(k => k.Yil == TabanYili && k.OyTuru == OyTuru.BelediyeMeclisi && (adaylar is null || adaylar.Contains(k.BirimKod)))
            .Select(k => (k.BirimKod, Oran: Oran(k)))
            .Where(x => x.Oran is not null)
            .OrderByDescending(x => x.Oran).ThenBy(x => x.BirimKod)
            .ToList();
        var adet = (int)Math.Ceiling(sirali.Count * KaleDilimi);
        return sirali.Take(adet).Select(x => x.BirimKod).ToList();
    }

    /// <param name="tabanYillari">Birim kodu → taban yılı (karşılaştırılabilir dönemin başı). Olmayan birim için 2009.</param>
    public static ErimeRaporu Hesapla(IEnumerable<KarneDegeri> degerler, OyTuru oyTuru, IReadOnlyCollection<int> kaleKodlari,
        IReadOnlyDictionary<int, int>? tabanYillari = null)
    {
        var seri = degerler.Where(k => k.OyTuru == oyTuru).ToList();
        int? sonYil = seri.Where(k => k.PusuladaMi).Select(k => (int?)k.Yil).Max();
        var kaleler = kaleKodlari.ToHashSet();

        var satirlar = seri
            .GroupBy(k => k.BirimKod)
            .Select(g =>
            {
                var tabanYili = tabanYillari?.GetValueOrDefault(g.Key, TabanYili) ?? TabanYili;
                var taban = g.SingleOrDefault(k => k.Yil == tabanYili);
                var son = sonYil is null ? null : g.SingleOrDefault(k => k.Yil == sonYil);
                return Satir(g.Key, g.Last().BirimAdi, tabanYili, taban, son, kaleler.Contains(g.Key));
            })
            // Farklı tabanlardan gelen kayıplar birbiriyle sıralanamaz: önce taban yılı (en uzun dönem önce), sonra kayıp.
            .OrderBy(s => s.TabanYili)
            .ThenByDescending(s => s.KayipOy.HasValue)
            .ThenByDescending(s => s.KayipOy)
            .ThenBy(s => s.BirimKod)
            .ToList();

        var kaleOrtalamalari = seri
            .Where(k => kaleler.Contains(k.BirimKod))
            .GroupBy(k => k.Yil)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                long toplam = g.Sum(k => k.ToplamOy);
                bool pusulada = g.All(k => k.PusuladaMi);
                long? dsp = pusulada ? g.Sum(k => k.DspOyu ?? 0) : null;
                return new KaleOrtalamasi(g.Key, g.Count(), toplam, dsp, Oran(toplam, dsp, pusulada));
            })
            .ToList();

        return new ErimeRaporu(oyTuru, TabanYili, sonYil, satirlar, kaleKodlari.ToList(), kaleOrtalamalari);
    }

    /// <summary>
    /// İlçenin tamamı (bütün birimlerin toplamı) için her seçimdeki oran ve 2009'a göre erime.
    /// İlçe sınırı 2009'dan beri değişmediği sürece uzun dönem karşılaştırma bu düzeyde güvenilirdir.
    /// </summary>
    public static IReadOnlyList<IlceErimeSatiri> IlceErimesi(IEnumerable<KarneDegeri> degerler, OyTuru oyTuru)
    {
        var yillar = degerler
            .Where(k => k.OyTuru == oyTuru)
            .GroupBy(k => k.Yil)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                long toplam = g.Sum(k => k.ToplamOy);
                bool pusulada = g.All(k => k.PusuladaMi);
                long? dsp = pusulada ? g.Sum(k => k.DspOyu ?? 0) : null;
                return (Yil: g.Key, Toplam: toplam, Dsp: dsp, Oran: Oran(toplam, dsp, pusulada));
            })
            .ToList();

        var taban = yillar.FirstOrDefault(y => y.Yil == TabanYili);
        return yillar.Select(y =>
        {
            decimal? tabanOrani = taban.Yil == TabanYili ? taban.Oran : null;
            decimal? beklenen = tabanOrani is not null && y.Oran is not null ? tabanOrani / 100m * y.Toplam : null;
            return new IlceErimeSatiri(y.Yil, y.Toplam, y.Dsp, y.Oran, y.Oran - tabanOrani,
                tabanOrani > 0 && y.Oran is not null ? y.Oran / tabanOrani : null,
                beklenen, beklenen - y.Dsp);
        }).ToList();
    }

    private static ErimeSatiri Satir(int kod, string ad, int tabanYili, KarneDegeri? taban, KarneDegeri? son, bool kaleMi)
    {
        var tabanOrani = Oran(taban);
        var sonOrani = Oran(son);

        decimal? korunma = tabanOrani > 0 && sonOrani is not null ? sonOrani / tabanOrani : null;
        decimal? beklenen = tabanOrani is not null && sonOrani is not null ? tabanOrani / 100m * son!.ToplamOy : null;
        decimal? kayip = beklenen is not null ? beklenen - son!.DspOyu : null;

        string? not =
            taban is null ? $"taban seçiminde ({tabanYili}) birim yok" :
            tabanOrani is null ? $"taban seçiminde ({tabanYili}) DSP pusulada değil" :
            tabanOrani == 0 ? $"taban seçiminde ({tabanYili}) oran 0; korunma oranı hesaplanamaz" :
            son is null ? "son seçimde yok" :
            null;

        return new ErimeSatiri(kod, ad, tabanYili,
            tabanOrani, taban?.ToplamOy, tabanOrani is null ? null : taban!.DspOyu,
            sonOrani, son?.ToplamOy, sonOrani is null ? null : son!.DspOyu,
            sonOrani - tabanOrani, korunma, beklenen, kayip, kaleMi, not);
    }
}
