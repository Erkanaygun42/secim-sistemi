using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Importer.Mahalleler;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Rapor;

/// <summary>
/// <c>rapor erime &lt;ilçe kodu&gt; [--oy-turu ...]</c>: seçilen seriyi (varsayılan belediye meclisi) ekrana yazar;
/// üç serinin csv'sini ve kale ortalamalarını <c>data/out</c> altına yazar. <c>data/</c> git'e girmez.
/// </summary>
internal static class RaporKomutlari
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly OyTuru[] Seriler = [OyTuru.BelediyeMeclisi, OyTuru.BelediyeBaskani, OyTuru.BuyuksehirBaskani];

    public static async Task<int> ErimeAsync(string ilceKodu, string? oyTuruAdi)
    {
        var gosterilen = OyTuru.BelediyeMeclisi;
        if (oyTuruAdi is not null && (!OyTuruAdlari.TryCoz(oyTuruAdi, out gosterilen) || !Seriler.Contains(gosterilen)))
            throw new YuklemeHatasi($"--oy-turu '{oyTuruAdi}' olamaz: {string.Join(", ", Seriler.Select(OyTuruAdlari.Ad))}.");

        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var ilce = await MahalleKomutlari.IlceBulAsync(db, ilceKodu);
        var servis = new ErimeRaporuServisi(db);
        var raporlar = new Dictionary<OyTuru, ErimeRaporu>();
        foreach (var seri in Seriler)
            raporlar[seri] = await servis.HazirlaAsync(ilce.Id, seri);

        var birlesikBirim = await db.KarsilastirmaBirimleri.CountAsync(b => b.IlceId == ilce.Id && b.Uyeler.Count > 1);
        var rapor = raporlar[gosterilen];

        Console.WriteLine($"ERİME RAPORU · {ilce.Ad} · {OyTuruAdlari.Ad(gosterilen)} · taban {rapor.TabanYili}, son seçim {rapor.SonYil?.ToString(CultureInfo.InvariantCulture) ?? "yok"}");
        Console.WriteLine("Oranlar yüzde; korunma = son oran ÷ 2009 oranı; beklenen = 2009 oranı × son toplam oy; kayıp = beklenen − gerçek. Sıra: kayıp oy.");
        if (birlesikBirim == 0)
            Console.WriteLine("UYARI: Onaylı mahalle eşlemesi yok; her mahalle ayrı birim. 2014–2019 arasında bölünen mahallelerin 2009 ve son seçimdeki toprağı aynı değil, yeni mahallelerin 2009 tabanı yok (docs/kararlar.md K6).");
        Console.WriteLine();
        ErimeTablosu(rapor);

        Console.WriteLine();
        Console.WriteLine($"2009 KALELERİ · 2009 belediye meclisi oranına göre en güçlü %{ErimeHesabi.KaleDilimi * 100:0} ({rapor.KaleKodlari.Count} birim): " +
                          string.Join(", ", rapor.KaleKodlari.Select(k => rapor.Satirlar.FirstOrDefault(s => s.BirimKod == k)?.BirimAdi ?? k.ToString(CultureInfo.InvariantCulture))));
        Console.WriteLine("Grubun ağırlıklı oranı (grubun DSP oyu ÷ grubun toplam oyu), yüzde:");
        KaleTablosu(raporlar);

        var klasor = Veritabani.DepoYolu("data", "out");
        Directory.CreateDirectory(klasor);
        var yazilan = new List<string>();
        foreach (var (seri, r) in raporlar)
        {
            var yol = Path.Combine(klasor, $"erime_{ilce.Kod}_{OyTuruAdlari.Ad(seri)}.csv");
            ErimeCsv(yol, r);
            yazilan.Add(yol);
        }
        var kaleYolu = Path.Combine(klasor, $"kaleler_{ilce.Kod}.csv");
        KaleCsv(kaleYolu, raporlar);
        yazilan.Add(kaleYolu);

        Console.WriteLine();
        Console.WriteLine("Yazılan dosyalar:");
        foreach (var y in yazilan)
            Console.WriteLine("  " + y);
        return 0;
    }

    private static string Sayi(decimal? d, string bicim = "N0") => d is { } v ? v.ToString(bicim, Turkce) : "—";
    private static string Sayi(long? d) => d is { } v ? v.ToString("N0", Turkce) : "—";

    private static void ErimeTablosu(ErimeRaporu r)
    {
        var sutunlar = new[] { "#", "Kod", "Birim", "2009 %", "Son %", "Puan", "Korunma", "2009 DSP", "Son DSP", "Son toplam", "Beklenen", "Kayıp", "Kale" };
        var satirlar = r.Satirlar.Select((s, i) => (IReadOnlyList<string>)
        [
            (i + 1).ToString(CultureInfo.InvariantCulture), s.BirimKod.ToString(CultureInfo.InvariantCulture), s.BirimAdi,
            Sayi(s.TabanOrani, "0.00"), Sayi(s.SonOrani, "0.00"), Sayi(s.PuanFarki, "+0.00;-0.00;0.00"), Sayi(s.KorunmaOrani, "0.00"),
            Sayi(s.TabanDspOyu), Sayi(s.SonDspOyu), Sayi(s.SonToplamOy), Sayi(s.BeklenenOy), Sayi(s.KayipOy), s.KaleMi ? "kale" : "",
        ]).ToList();
        Tablo.Yaz(sutunlar, satirlar, sagaYasli: new HashSet<int> { 0, 1, 3, 4, 5, 6, 7, 8, 9, 10, 11 });

        var notlar = r.Satirlar.Where(s => s.Not is not null).GroupBy(s => s.Not!).ToList();
        foreach (var g in notlar)
            Console.WriteLine($"  Not ({g.Count()} birim): {g.Key}");
    }

    private static void KaleTablosu(Dictionary<OyTuru, ErimeRaporu> raporlar)
    {
        var yillar = raporlar.Values.SelectMany(r => r.KaleOrtalamalari.Select(k => k.Yil)).Distinct().Order().ToList();
        var sutunlar = new List<string> { "Seri" };
        sutunlar.AddRange(yillar.Select(y => y.ToString(CultureInfo.InvariantCulture)));
        var satirlar = raporlar.Select(kv => (IReadOnlyList<string>)new List<string>(
            [OyTuruAdlari.Ad(kv.Key), .. yillar.Select(y => Sayi(kv.Value.KaleOrtalamalari.FirstOrDefault(k => k.Yil == y)?.Oran, "0.00"))])).ToList();
        Tablo.Yaz(sutunlar, satirlar, sagaYasli: Enumerable.Range(1, yillar.Count).ToHashSet());
    }

    // ---- csv: UTF-8 BOM, ';' ayraç, ondalıkta virgül (Excel tr-TR) ----

    private static string Csv(decimal? d) => d?.ToString("0.######", Turkce) ?? "";
    private static string Csv(long? d) => d?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string Csv(string? s) => (s ?? "").Replace(';', ',');

    private static void CsvYaz(string yol, IEnumerable<string[]> satirlar)
    {
        using var yazici = new StreamWriter(yol, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        foreach (var s in satirlar)
            yazici.WriteLine(string.Join(';', s));
    }

    private static void ErimeCsv(string yol, ErimeRaporu r)
    {
        string[] baslik = ["sira", "birim_kod", "birim_adi", "taban_yili", "taban_orani", "taban_toplam_oy", "taban_dsp_oyu",
            "son_yil", "son_orani", "son_toplam_oy", "son_dsp_oyu", "puan_farki", "korunma_orani", "beklenen_oy", "kayip_oy", "kale", "not"];
        CsvYaz(yol, r.Satirlar.Select((s, i) => new[]
        {
            (i + 1).ToString(CultureInfo.InvariantCulture), s.BirimKod.ToString(CultureInfo.InvariantCulture), Csv(s.BirimAdi),
            r.TabanYili.ToString(CultureInfo.InvariantCulture), Csv(s.TabanOrani), Csv(s.TabanToplamOy), Csv(s.TabanDspOyu),
            r.SonYil?.ToString(CultureInfo.InvariantCulture) ?? "", Csv(s.SonOrani), Csv(s.SonToplamOy), Csv(s.SonDspOyu),
            Csv(s.PuanFarki), Csv(s.KorunmaOrani), Csv(s.BeklenenOy), Csv(s.KayipOy), s.KaleMi ? "evet" : "hayir", Csv(s.Not),
        }).Prepend(baslik));
    }

    private static void KaleCsv(string yol, Dictionary<OyTuru, ErimeRaporu> raporlar)
    {
        string[] baslik = ["seri", "yil", "birim_sayisi", "toplam_oy", "dsp_oyu", "oran"];
        CsvYaz(yol, raporlar.SelectMany(kv => kv.Value.KaleOrtalamalari.Select(k => new[]
        {
            OyTuruAdlari.Ad(kv.Key), k.Yil.ToString(CultureInfo.InvariantCulture), k.BirimSayisi.ToString(CultureInfo.InvariantCulture),
            Csv(k.ToplamOy), Csv(k.DspOyu), Csv(k.Oran),
        })).Prepend(baslik));
    }
}
