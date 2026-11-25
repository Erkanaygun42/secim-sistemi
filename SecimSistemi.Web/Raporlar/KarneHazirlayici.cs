using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Karnenin adresteki filtreleri: <c>?ilce=34-ESENYURT&amp;secim=4&amp;oy-turu=belediye-meclisi&amp;sirala=dsp-orani&amp;yon=azalan</c>.
/// Sayfa, Excel ve PDF aynı parametreleri kullanır; böylece dosyada ekrandakiyle aynı tablo çıkar.
/// </summary>
public sealed record KarneSorgusu(string? Ilce, int? Secim, string? OyTuru, string? Sirala, string? Yon)
{
    public static readonly IReadOnlyDictionary<string, KarneSiraAlani> SiraKodlari = new Dictionary<string, KarneSiraAlani>
    {
        ["kod"] = KarneSiraAlani.Kod,
        ["ad"] = KarneSiraAlani.Ad,
        ["sandik"] = KarneSiraAlani.Sandik,
        ["toplam-oy"] = KarneSiraAlani.ToplamOy,
        ["dsp-oyu"] = KarneSiraAlani.DspOyu,
        ["dsp-orani"] = KarneSiraAlani.DspOrani,
        ["puan-farki"] = KarneSiraAlani.PuanFarki,
        ["sinir"] = KarneSiraAlani.Sinir,
    };

    public static string Kod(KarneSiraAlani alan) => SiraKodlari.First(x => x.Value == alan).Key;

    public static KarneSorgusu Oku(IQueryCollection q) =>
        new(q["ilce"], int.TryParse(q["secim"], out var s) ? s : null, q["oy-turu"], q["sirala"], q["yon"]);

    /// <summary>Adres parametreleri (NavigationManager.GetUriWithQueryParameters ve dışa aktarma bağlantıları için).</summary>
    public Dictionary<string, object?> Parametreler() => new()
    {
        ["ilce"] = Ilce, ["secim"] = Secim, ["oy-turu"] = OyTuru, ["sirala"] = Sirala, ["yon"] = Yon,
    };

    public string SorguMetni() =>
        string.Join("&", Parametreler().Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, Metinler.Turkce)!)}"));
}

/// <summary>Filtreleri çözülmüş karne: seçenek listeleri, seçilenler ve sıralanmış satırlar.</summary>
/// <param name="Karne">İlçe ya da seçim yoksa boş.</param>
/// <param name="Sorgu">Varsayılanları doldurulmuş sorgu (seçilenler).</param>
public sealed record KarneSonucu(
    IReadOnlyList<IlceSecenegi> Ilceler, IReadOnlyList<SecimSecenegi> Secimler, IReadOnlyList<OyTuru> OyTurleri,
    Karne? Karne, KarneSiraAlani Alan, bool Azalan, KarneSorgusu Sorgu)
{
    public string FiltreOzeti => Karne is null ? "" :
        $"{Karne.Ilce.IlAdi} / {Karne.Ilce.Ad} · {Karne.Secim.Ad} ({Karne.Secim.Tarih.ToString("d MMMM yyyy", Metinler.Turkce)}) · " +
        $"{Metinler.Ad(Karne.OyTuru)} · sıra: {SiraAdi(Alan)} {(Azalan ? "azalan" : "artan")}";

    public static string SiraAdi(KarneSiraAlani a) => a switch
    {
        KarneSiraAlani.Kod => "kod",
        KarneSiraAlani.Ad => "ad",
        KarneSiraAlani.Sandik => "sandık sayısı",
        KarneSiraAlani.ToplamOy => "toplam oy",
        KarneSiraAlani.DspOyu => "DSP oyu",
        KarneSiraAlani.DspOrani => "DSP oranı",
        KarneSiraAlani.PuanFarki => "puan farkı",
        KarneSiraAlani.Sinir => "sınır durumu",
        _ => a.ToString(),
    };
}

/// <summary>
/// Adresteki filtrelerden karneyi hazırlar. Eksik ya da geçersiz filtre varsayılana döner:
/// ilk ilçe, en son seçim, belediye meclisi (yoksa ilk oy türü), DSP oranı azalan.
/// </summary>
public sealed class KarneHazirlayici(IDbContextFactory<SecimDbContext> fabrika)
{
    public async Task<KarneSonucu> HazirlaAsync(KarneSorgusu sorgu, CancellationToken ct = default)
    {
        await using var db = await fabrika.CreateDbContextAsync(ct);
        var servis = new KarneServisi(db);

        var alan = sorgu.Sirala is not null && KarneSorgusu.SiraKodlari.TryGetValue(sorgu.Sirala, out var a) ? a : KarneSiraAlani.DspOrani;
        var azalan = sorgu.Yon != "artan";

        var ilceler = await servis.IlcelerAsync(ct);
        var ilce = ilceler.FirstOrDefault(i => i.Kod == sorgu.Ilce) ?? ilceler.FirstOrDefault();
        if (ilce is null)
            return new KarneSonucu(ilceler, [], [], null, alan, azalan, sorgu);

        var secimler = await servis.SecimlerAsync(ilce.Id, ct);
        var secim = secimler.FirstOrDefault(s => s.Id == sorgu.Secim) ?? secimler.FirstOrDefault();
        if (secim is null)
            return new KarneSonucu(ilceler, secimler, [], null, alan, azalan, sorgu with { Ilce = ilce.Kod });

        var oyTurleri = await servis.OyTurleriAsync(ilce.Id, secim.Id, ct);
        var istenen = Metinler.OyTuruCoz(sorgu.OyTuru);
        var oyTuru = istenen is { } o && oyTurleri.Contains(o) ? o
            : oyTurleri.Contains(OyTuru.BelediyeMeclisi) ? OyTuru.BelediyeMeclisi : oyTurleri[0];

        var karne = await servis.KarneAsync(ilce, secim, oyTuru, ct);
        karne = karne with { Satirlar = KarneSiralamasi.Uygula(karne.Satirlar, alan, azalan) };

        var cozulmus = new KarneSorgusu(ilce.Kod, secim.Id, Metinler.Kod(oyTuru), KarneSorgusu.Kod(alan), azalan ? "azalan" : "artan");
        return new KarneSonucu(ilceler, secimler, oyTurleri, karne, alan, azalan, cozulmus);
    }

    /// <summary>Mahalle detayı; ilçe ya da birim yoksa boş.</summary>
    public async Task<MahalleDetayi?> MahalleAsync(string ilceKod, int birimKod, CancellationToken ct = default)
    {
        await using var db = await fabrika.CreateDbContextAsync(ct);
        var servis = new KarneServisi(db);
        return await servis.IlceAsync(ilceKod, ct) is { } ilce ? await servis.MahalleAsync(ilce, birimKod, ct) : null;
    }
}
