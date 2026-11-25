using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Gruplar ve öncelik sayfalarının adresteki filtreleri:
/// <c>?ilce=34-ESENYURT&amp;oy-turu=belediye-meclisi&amp;grup=erozyon&amp;donem=2009</c>.
/// <c>grup</c> ve <c>donem</c> (dönemin başladığı yıl) yalnızca öncelik listesini süzer; boşsa hepsi.
/// </summary>
public sealed record GrupSorgusu(string? Ilce, string? OyTuru, string? Grup = null, int? Donem = null)
{
    public static readonly IReadOnlyDictionary<string, MahalleGrubu> GrupKodlari = new Dictionary<string, MahalleGrubu>
    {
        ["kale"] = MahalleGrubu.Kale,
        ["erozyon"] = MahalleGrubu.Erozyon,
        ["yukselen"] = MahalleGrubu.Yukselen,
        ["zayif"] = MahalleGrubu.Zayif,
    };

    public static string Kod(MahalleGrubu g) => GrupKodlari.First(x => x.Value == g).Key;

    public MahalleGrubu? GrupSecimi => Grup is not null && GrupKodlari.TryGetValue(Grup, out var g) ? g : null;

    public static GrupSorgusu Oku(IQueryCollection q) =>
        new(q["ilce"], q["oy-turu"], q["grup"], int.TryParse(q["donem"], out var d) ? d : null);

    public Dictionary<string, object?> Parametreler() => new()
    {
        ["ilce"] = Ilce, ["oy-turu"] = OyTuru, ["grup"] = Grup, ["donem"] = Donem,
    };

    public string SorguMetni() =>
        string.Join("&", Parametreler().Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, Metinler.Turkce)!)}"));
}

/// <summary>2x2 tablonun bir karesi: bugünkü güç (ilçenin üstünde mi) × eğilim (dönem içi puan farkı ≥ 0 mı).</summary>
public sealed record DortluKare(bool Guclu, bool Yukseldi, IReadOnlyList<GrupSatiri> Mahalleler)
{
    public int Sayi => Mahalleler.Count;
    public long ToplamOy => Mahalleler.Sum(m => m.SonToplamOy ?? 0);

    public string Ad => (Guclu, Yukseldi) switch
    {
        (true, true) => "Güçlü ve yükselen",
        (true, false) => "Güçlü ama gerileyen",
        (false, true) => "Zayıf ama yükselen",
        (false, false) => "Zayıf ve gerileyen",
    };
}

/// <param name="Gruplar">Seçili serinin grupları; ilçe ya da veri yoksa boş.</param>
/// <param name="Oncelik">Bütün dönemlerin öncelik listeleri (süzülmemiş).</param>
public sealed record GrupSonuclari(ErimeSonucu Erime, GrupSonucu? Gruplar, IReadOnlyList<OncelikListesi> Oncelik, GrupSorgusu Sorgu)
{
    public IlceSecenegi? Ilce => Erime.Ilce;

    /// <summary>2x2 tablo: güçlü/zayıf × yükselen/gerileyen. Bugünkü güç ya da puan farkı olmayan mahalle girmez.</summary>
    public IReadOnlyList<DortluKare> Kareler()
    {
        var satirlar = (Gruplar?.Satirlar ?? []).Where(s => s.BugunkuGuc is not null && s.PuanFarki is not null).ToList();
        return [.. from guclu in new[] { true, false }
                   from yukseldi in new[] { true, false }
                   select new DortluKare(guclu, yukseldi,
                       satirlar.Where(s => s.BugunkuGuc >= 0 == guclu && s.PuanFarki >= 0 == yukseldi).ToList())];
    }

    /// <summary>Adresteki grup ve dönem süzgecinden geçen öncelik listeleri; boş kalan dönem listesi çıkarılır.</summary>
    public IReadOnlyList<OncelikListesi> SuzulmusOncelik() =>
        Oncelik
            .Where(l => Sorgu.Donem is null || l.DonemBaslangici == Sorgu.Donem)
            .Select(l => l with { Satirlar = [.. l.Satirlar.Where(s => Sorgu.GrupSecimi is null || s.Grup.Grup == Sorgu.GrupSecimi)] })
            .Where(l => l.Satirlar.Count > 0)
            .ToList();

    public string FiltreOzeti => Ilce is null ? "" :
        $"{Ilce.IlAdi} / {Ilce.Ad} · {Metinler.Ad(Erime.OyTuru)} · son seçim {Erime.Rapor?.SonYil?.ToString() ?? Metinler.Bos} · " +
        $"değişim eşiği: {GrupAyarlari.Metin(Gruplar?.Esikler ?? GrupEsikleri.Varsayilan)}";

    public string SuzgecOzeti =>
        (Sorgu.GrupSecimi is { } g ? $"grup: {Metinler.Ad(g)}" : "bütün gruplar") + " · " +
        (Sorgu.Donem is { } d ? $"dönem: {Oncelik.FirstOrDefault(l => l.DonemBaslangici == d)?.Donem ?? d.ToString(Metinler.Turkce)}" : "bütün dönemler");
}

/// <summary>Grupları ve öncelik listesini hazırlar; erime raporunu (aynı servisler, aynı sınır analizi) temel alır.</summary>
public sealed class GrupHazirlayici(ErimeHazirlayici erime, GrupEsikleri esikler)
{
    public async Task<GrupSonuclari> HazirlaAsync(GrupSorgusu sorgu, CancellationToken ct = default)
    {
        var e = await erime.HazirlaAsync(new ErimeSorgusu(sorgu.Ilce, sorgu.OyTuru), ct);
        var cozulmus = sorgu with { Ilce = e.Sorgu.Ilce, OyTuru = e.Sorgu.OyTuru, Grup = sorgu.GrupSecimi is { } g ? GrupSorgusu.Kod(g) : null };
        if (e.Rapor is not { } rapor || !e.IlceErimesi.TryGetValue(e.OyTuru, out var ilce))
            return new GrupSonuclari(e, null, [], cozulmus);

        var gruplar = GrupHesabi.Hesapla(rapor, ilce.ToDictionary(x => x.Yil, x => x.Oran), esikler);
        return new GrupSonuclari(e, gruplar, OncelikHesabi.Hesapla(gruplar, e.Sinir), cozulmus);
    }
}
