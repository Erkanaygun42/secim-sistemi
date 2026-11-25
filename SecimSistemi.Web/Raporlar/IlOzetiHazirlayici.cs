using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>İl özetinin sıralanabilir sütunları.</summary>
public enum IlSiraAlani { Ad, DspOrani, Puan, Korunma, Kayip }

/// <summary>İl özetinin adresteki filtreleri: <c>?il=İSTANBUL&amp;oy-turu=belediye-meclisi&amp;sirala=kayip&amp;yon=azalan</c>.</summary>
public sealed record IlOzetiSorgusu(string? Il, string? OyTuru, string? Sirala = null, string? Yon = null)
{
    public static readonly IReadOnlyDictionary<string, IlSiraAlani> SiraKodlari = new Dictionary<string, IlSiraAlani>
    {
        ["ad"] = IlSiraAlani.Ad, ["dsp-orani"] = IlSiraAlani.DspOrani, ["puan"] = IlSiraAlani.Puan,
        ["korunma"] = IlSiraAlani.Korunma, ["kayip"] = IlSiraAlani.Kayip,
    };

    public static string Kod(IlSiraAlani a) => SiraKodlari.First(x => x.Value == a).Key;

    public static IlOzetiSorgusu Oku(IQueryCollection q) => new(q["il"], q["oy-turu"], q["sirala"], q["yon"]);

    public Dictionary<string, object?> Parametreler() => new() { ["il"] = Il, ["oy-turu"] = OyTuru, ["sirala"] = Sirala, ["yon"] = Yon };

    public string SorguMetni() =>
        string.Join("&", Parametreler().Where(p => p.Value is not null).Select(p => $"{p.Key}={Uri.EscapeDataString((string)p.Value!)}"));
}

/// <summary>Bir ilçenin il özetindeki satırı: ilçenin tamamı için taban yılı (2009) ve son seçim.</summary>
/// <param name="Son">Son seçim: o seride DSP'nin pusulada olduğu en son yerel seçim; yoksa en son seçim.</param>
/// <param name="SupheliMahalle">Son seçimde dönemi şüpheli (sınır kararı girilmemiş) mahalle sayısı.</param>
public sealed record IlceOzeti(IlceSecenegi Ilce, IlceErimeSatiri? Taban, IlceErimeSatiri? Son, int MahalleSayisi, int SupheliMahalle)
{
    public decimal? Oran => Son?.Oran;
    public decimal? Puan => Son?.PuanFarki;
    public decimal? Korunma => Son?.KorunmaOrani;
    public decimal? Kayip => Son?.KayipOy;
}

/// <param name="Toplam">Yüklü ilçelerin toplamı (il geneli değil, tanımlı ilçe sayısı kadar).</param>
public sealed record IlOzeti(
    IReadOnlyList<string> Iller, string? Il, IReadOnlyList<OyTuru> Seriler, OyTuru OyTuru,
    IReadOnlyList<IlceOzeti> Ilceler, IlceErimeSatiri? ToplamTaban, IlceErimeSatiri? ToplamSon,
    IlSiraAlani Alan, bool Azalan, IlOzetiSorgusu Sorgu)
{
    /// <summary>İlin bilinen ilçe sayısı (uyarı için); bilinmiyorsa boş.</summary>
    public int? IlinIlceSayisi => Il is null ? null : IlOzetiHazirlayici.IlceSayilari.GetValueOrDefault(Il);

    /// <summary>Yalnızca bazı ilçeler yüklüyse uyarı: il toplamı il geneli değildir.</summary>
    public string? KapsamUyarisi => IlinIlceSayisi is { } n && Ilceler.Count < n
        ? $"{Il}: {n} ilçeden yüklü olan {Ilceler.Count} ilçe ({string.Join(", ", Ilceler.Select(i => i.Ilce.Ad).Order(Siralama.Turkce))}). " +
          "Toplam satırı yalnızca yüklü ilçelerin toplamıdır, il geneli değildir."
        : null;

    public string FiltreOzeti => Il is null ? "" :
        $"{Il} · {Metinler.Ad(OyTuru)} · {Ilceler.Count} ilçe · taban {ErimeHesabi.TabanYili} · sıra: {SiraAdi(Alan)} {(Azalan ? "azalan" : "artan")}";

    public static string SiraAdi(IlSiraAlani a) => a switch
    {
        IlSiraAlani.Ad => "ilçe adı",
        IlSiraAlani.DspOrani => "DSP oranı",
        IlSiraAlani.Puan => "puan farkı",
        IlSiraAlani.Korunma => "korunma",
        IlSiraAlani.Kayip => "kayıp oy",
        _ => a.ToString(),
    };
}

/// <summary>
/// İl özeti: ildeki yüklü ilçeleri DSP oranına, erimeye (2009'a göre puan farkı ve korunma) ve kayıp oya göre sıralar.
/// Değerler erime raporunun ilçe tablosuyla aynı hesaptır (<see cref="ErimeHesabi.IlceErimesi"/>). Yeni ilçe yüklenince
/// kendiliğinden görünür.
/// </summary>
public sealed class IlOzetiHazirlayici(IDbContextFactory<SecimDbContext> fabrika)
{
    /// <summary>İllerin ilçe sayısı (kapsam uyarısı için). Yeni il eklendiğinde buraya yazılır.</summary>
    public static readonly IReadOnlyDictionary<string, int> IlceSayilari = new Dictionary<string, int>
    {
        ["İSTANBUL"] = 39,
        ["KOCAELİ"] = 12,
    };

    private static readonly OyTuru[] YerelSeriler = [OyTuru.BelediyeMeclisi, OyTuru.BelediyeBaskani, OyTuru.BuyuksehirBaskani, OyTuru.IlGenelMeclisi];

    public async Task<IlOzeti> HazirlaAsync(IlOzetiSorgusu sorgu, CancellationToken ct = default)
    {
        await using var db = await fabrika.CreateDbContextAsync(ct);
        return await HazirlaAsync(db, sorgu, ct);
    }

    /// <summary>Verilen bağlantıyla (testler aynı transaction içinde çağırır).</summary>
    public static async Task<IlOzeti> HazirlaAsync(SecimDbContext db, IlOzetiSorgusu sorgu, CancellationToken ct = default)
    {
        var karne = new KarneServisi(db);
        var tumu = await karne.IlcelerAsync(ct);
        var iller = tumu.Select(i => i.IlAdi).Distinct().ToList();
        var il = iller.FirstOrDefault(x => x == sorgu.Il) ?? iller.FirstOrDefault();
        var alan = sorgu.Sirala is not null && IlOzetiSorgusu.SiraKodlari.TryGetValue(sorgu.Sirala, out var a) ? a : IlSiraAlani.Kayip;
        var azalan = sorgu.Yon is null ? alan != IlSiraAlani.Ad : sorgu.Yon != "artan";

        var ilceler = tumu.Where(i => i.IlAdi == il).ToList();
        var ids = ilceler.Select(i => i.Id).ToList();
        var mevcut = await db.MahalleKarnesi.AsNoTracking()
            .Where(k => ids.Contains(k.IlceId) && k.SecimTuru == SecimTuru.Yerel)
            .Select(k => k.OyTuru).Distinct().ToListAsync(ct);
        var seriler = YerelSeriler.Where(mevcut.Contains).ToList();
        var oyTuru = Metinler.OyTuruCoz(sorgu.OyTuru) is { } o && seriler.Contains(o) ? o : seriler.FirstOrDefault();

        var servis = new ErimeRaporuServisi(db);
        var ozetler = new List<IlceOzeti>();
        var hamlar = new List<IReadOnlyList<KarneDegeri>>();
        foreach (var ilce in ilceler)
        {
            var degerler = await db.MahalleKarnesi.AsNoTracking()
                .Where(k => k.IlceId == ilce.Id && k.SecimTuru == SecimTuru.Yerel && k.OyTuru == oyTuru)
                .Select(k => new KarneDegeri(k.BirimKod, k.BirimAdi, k.Yil, k.OyTuru, k.ToplamOy, k.DspOyu, k.PusuladaMi))
                .ToListAsync(ct);
            if (degerler.Count == 0) continue;
            hamlar.Add(degerler);
            var satirlar = ErimeHesabi.IlceErimesi(degerler, oyTuru);
            var sinir = await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, KarneServisi.Esikler, ct: ct);
            var son = SonSatir(satirlar);
            ozetler.Add(new IlceOzeti(ilce, satirlar.FirstOrDefault(s => s.Yil == ErimeHesabi.TabanYili), son,
                son is null ? 0 : degerler.Count(d => d.Yil == son.Yil),
                sinir.Birimler.Count(b => b.SupheliVar)));
        }

        // Toplam: yüklü ilçelerin bütün mahallelerinin toplamı (aynı hesap, birim kodları ilçeler arasında çakışmaz).
        var toplam = ErimeHesabi.IlceErimesi(hamlar.SelectMany(h => h), oyTuru);
        var cozulmus = new IlOzetiSorgusu(il, seriler.Count == 0 ? null : Metinler.Kod(oyTuru), IlOzetiSorgusu.Kod(alan), azalan ? "azalan" : "artan");
        return new IlOzeti(iller, il, seriler, oyTuru, Sirala(ozetler, alan, azalan),
            toplam.FirstOrDefault(s => s.Yil == ErimeHesabi.TabanYili), SonSatir(toplam), alan, azalan, cozulmus);
    }

    /// <summary>Son seçim: DSP'nin pusulada olduğu en son yıl; hiç yoksa en son yıl.</summary>
    public static IlceErimeSatiri? SonSatir(IReadOnlyList<IlceErimeSatiri> satirlar) =>
        satirlar.LastOrDefault(s => s.Oran is not null) ?? satirlar.LastOrDefault();

    /// <summary>Boş değerler iki yönde de sonda; eşitlikte ilçe adı (Türkçe alfabe).</summary>
    public static IReadOnlyList<IlceOzeti> Sirala(IEnumerable<IlceOzeti> ozetler, IlSiraAlani alan, bool azalan)
    {
        var l = ozetler.ToList();
        if (alan == IlSiraAlani.Ad)
            return azalan ? [.. l.OrderByDescending(x => x.Ilce.Ad, Siralama.Turkce)] : [.. l.OrderBy(x => x.Ilce.Ad, Siralama.Turkce)];
        decimal? Anahtar(IlceOzeti x) => alan switch
        {
            IlSiraAlani.DspOrani => x.Oran,
            IlSiraAlani.Puan => x.Puan,
            IlSiraAlani.Korunma => x.Korunma,
            _ => x.Kayip,
        };
        var dolu = l.Where(x => Anahtar(x) is not null);
        var sirali = azalan ? dolu.OrderByDescending(Anahtar) : dolu.OrderBy(Anahtar);
        return [.. sirali.ThenBy(x => x.Ilce.Ad, Siralama.Turkce), .. l.Where(x => Anahtar(x) is null).OrderBy(x => x.Ilce.Ad, Siralama.Turkce)];
    }
}
