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
/// <c>rapor erime</c> ve <c>rapor esik-dogrulama</c>. csv'ler <c>data/out</c> altına yazılır; <c>data/</c> git'e girmez.
/// </summary>
internal static class RaporKomutlari
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly OyTuru[] Seriler = [OyTuru.BelediyeMeclisi, OyTuru.BelediyeBaskani, OyTuru.BuyuksehirBaskani];

    // ================= rapor erime =================

    public static async Task<int> ErimeAsync(string ilceKodu, string? oyTuruAdi, SinirEsikleri esikler)
    {
        var gosterilen = OyTuru.BelediyeMeclisi;
        if (oyTuruAdi is not null && (!OyTuruAdlari.TryCoz(oyTuruAdi, out gosterilen) || !Seriler.Contains(gosterilen)))
            throw new YuklemeHatasi($"--oy-turu '{oyTuruAdi}' olamaz: {string.Join(", ", Seriler.Select(OyTuruAdlari.Ad))}.");

        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var ilce = await MahalleKomutlari.IlceBulAsync(db, ilceKodu);
        var sinir = await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, esikler);
        var servis = new ErimeRaporuServisi(db);
        var raporlar = new Dictionary<OyTuru, ErimeRaporu>();
        var ilceErimeleri = new Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>>();
        foreach (var seri in Seriler)
        {
            raporlar[seri] = await servis.HazirlaAsync(ilce.Id, seri, sinir);
            ilceErimeleri[seri] = await servis.IlceErimesiAsync(ilce.Id, seri);
        }

        // ---- 1. İlçe düzeyi, uzun dönem ----
        Console.WriteLine($"İLÇE DÜZEYİ ERİME · {ilce.Ad} · taban {ErimeHesabi.TabanYili} (ilçe sınırı bu dönemde değişmedi)");
        IlceTablosu(ilceErimeleri);

        // ---- 2. Mahalle (birim) düzeyi, karşılaştırılabilir dönemlerle ----
        var rapor = raporlar[gosterilen];
        Console.WriteLine();
        Console.WriteLine($"MAHALLE DÜZEYİ ERİME · {ilce.Ad} · {OyTuruAdlari.Ad(gosterilen)} · son seçim {rapor.SonYil?.ToString(CultureInfo.InvariantCulture) ?? "yok"}");
        Console.WriteLine($"Her birimin tabanı kendi karşılaştırılabilir döneminin (son sınır değişikliğinden bugüne) ilk seçimidir. Sınır eşikleri: {MahalleKomutlari.EsikMetni(esikler)}");
        Console.WriteLine("Korunma = son oran ÷ taban oranı; beklenen = taban oranı × son toplam oy; kayıp = beklenen − gerçek. Sıra: dönem (en uzun önce), sonra kayıp oy.");
        Console.WriteLine();
        ErimeTablosu(rapor, sinir);

        // ---- 3. Kaleler ----
        Console.WriteLine();
        var kaleAdlari = rapor.KaleKodlari.Select(k => sinir.Birim(k)?.BirimAdi ?? k.ToString(CultureInfo.InvariantCulture));
        var sabit2009 = sinir.Birimler.Count(b => b.DonemBaslangici == ErimeHesabi.TabanYili);
        Console.WriteLine($"2009 KALELERİ · 2009'dan beri karşılaştırılabilir {sabit2009} birim içinde 2009 meclis oranına göre en güçlü %{ErimeHesabi.KaleDilimi * 100:0} " +
                          $"({rapor.KaleKodlari.Count} birim): {string.Join(", ", kaleAdlari)}");
        Console.WriteLine("Grubun ağırlıklı oranı (grubun DSP oyu ÷ grubun toplam oyu), yüzde:");
        KaleTablosu(raporlar);

        // ---- csv ----
        var klasor = Veritabani.DepoYolu("data", "out");
        Directory.CreateDirectory(klasor);
        var yazilan = new List<string>();
        foreach (var (seri, r) in raporlar)
            yazilan.Add(ErimeCsv(Path.Combine(klasor, $"erime_{ilce.Kod}_{OyTuruAdlari.Ad(seri)}.csv"), r, sinir));
        yazilan.Add(KaleCsv(Path.Combine(klasor, $"kaleler_{ilce.Kod}.csv"), raporlar));
        yazilan.Add(IlceCsv(Path.Combine(klasor, $"erime-ilce_{ilce.Kod}.csv"), ilceErimeleri));
        yazilan.Add(SinirCsv(Path.Combine(klasor, $"sinir_{ilce.Kod}.csv"), sinir));

        Console.WriteLine();
        Console.WriteLine("Yazılan dosyalar:");
        foreach (var y in yazilan)
            Console.WriteLine("  " + y);
        return 0;
    }

    /// <summary>Birimin dönem içindeki sınır uyarısı: şüpheli seçimler ve insan kararıyla doğrulananlar.</summary>
    internal static string SinirUyarisi(BirimDonemi? b)
    {
        if (b is null) return "";
        var parcalar = new List<string>();
        var supheli = b.Yillar.Where(y => y.Sonuc == SinirDurumu.Supheli).ToList();
        if (supheli.Count > 0)
            parcalar.Add("⚠ şüpheli " + string.Join(", ", supheli.Select(y => string.Create(Turkce, $"{y.Yil} ({y.PayDegisimi * 100:+0;-0}%)"))));
        var dogrulanan = b.Yillar.Where(y => y.Dogrulandi).ToList();
        if (dogrulanan.Count > 0)
            parcalar.Add("✓ doğrulandı " + string.Join(", ", dogrulanan.Select(y => $"{y.Yil} {MahalleKomutlari.DurumAdi(y.Sonuc)}")));
        return string.Join("; ", parcalar);
    }

    private static string Sayi(decimal? d, string bicim = "N0") => d is { } v ? v.ToString(bicim, Turkce) : "—";
    private static string Sayi(long? d) => d is { } v ? v.ToString("N0", Turkce) : "—";

    private static void IlceTablosu(Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>> ilce)
    {
        string[] sutunlar = ["Seri", "Yıl", "Toplam oy", "DSP oyu", "Oran %", "Puan (2009'a göre)", "Korunma", "Beklenen", "Kayıp"];
        var satirlar = ilce.SelectMany(kv => kv.Value.Select(s => (IReadOnlyList<string>)
        [
            OyTuruAdlari.Ad(kv.Key), s.Yil.ToString(CultureInfo.InvariantCulture), Sayi(s.ToplamOy), Sayi(s.DspOyu), Sayi(s.Oran, "0.00"),
            Sayi(s.PuanFarki, "+0.00;-0.00;0.00"), Sayi(s.KorunmaOrani, "0.00"), Sayi(s.BeklenenOy), Sayi(s.KayipOy),
        ])).ToList();
        Tablo.Yaz(sutunlar, satirlar, sagaYasli: Enumerable.Range(1, 8).ToHashSet());
    }

    private static void ErimeTablosu(ErimeRaporu r, SinirAnaliziSonucu sinir)
    {
        string[] sutunlar = ["#", "Kod", "Birim", "Dönem", "Taban %", "Son %", "Puan", "Korunma", "Taban DSP", "Son DSP", "Son toplam", "Beklenen", "Kayıp", "Kale", "Sınır"];
        var satirlar = r.Satirlar.Select((s, i) =>
        {
            var b = sinir.Birim(s.BirimKod);
            return (IReadOnlyList<string>)
            [
                (i + 1).ToString(CultureInfo.InvariantCulture), s.BirimKod.ToString(CultureInfo.InvariantCulture), s.BirimAdi, b?.Donem ?? "—",
                Sayi(s.TabanOrani, "0.00"), Sayi(s.SonOrani, "0.00"), Sayi(s.PuanFarki, "+0.00;-0.00;0.00"), Sayi(s.KorunmaOrani, "0.00"),
                Sayi(s.TabanDspOyu), Sayi(s.SonDspOyu), Sayi(s.SonToplamOy), Sayi(s.BeklenenOy), Sayi(s.KayipOy), s.KaleMi ? "kale" : "",
                SinirUyarisi(b),
            ];
        }).ToList();
        Tablo.Yaz(sutunlar, satirlar, sagaYasli: new HashSet<int> { 0, 1, 4, 5, 6, 7, 8, 9, 10, 11, 12 });

        foreach (var g in r.Satirlar.Where(s => s.Not is not null).GroupBy(s => s.Not!))
            Console.WriteLine($"  Not ({g.Count()} birim): {g.Key}");
        foreach (var g in r.Satirlar.GroupBy(s => sinir.Birim(s.BirimKod)?.Donem ?? "—").OrderBy(g => g.Key))
            Console.WriteLine($"  Dönem {g.Key}: {g.Count()} birim");
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

    // ================= rapor esik-dogrulama =================

    /// <summary>
    /// Onaylı insan kararlarını (Sabit / SiniriDegisti; eşlemede eski mahalle, yeni mahallenin kurulduğu seçimde SiniriDegisti
    /// sayılır) mahalle düzeyindeki otomatik tespitle karşılaştırır. Eşikleri yeniden ayarlarken kullanılır.
    /// </summary>
    public static async Task<int> EsikDogrulamaAsync(string ilceKodu, SinirEsikleri esikler)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var ilce = await MahalleKomutlari.IlceBulAsync(db, ilceKodu);
        var sinir = await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, esikler, birimDuzeyinde: false);

        var kararlar = await db.MahalleDurumKararlari.AsNoTracking()
            .Include(k => k.MahalleKoy).Include(k => k.IliskiliMahalleKoy).Include(k => k.Secim)
            .Where(k => k.MahalleKoy.IlceId == ilce.Id && k.Durum == KararDurumu.Onaylandi && k.YerineGecenKararId == null)
            .ToListAsync();

        // Yeni mahallelerin ilk göründüğü yıl (eşleme kararını seçime çevirmek için).
        var ilkYillar = sinir.Birimler.ToDictionary(b => b.BirimKod, b => b.Yillar.FirstOrDefault(y => y.Otomatik != SinirDurumu.SonradanKuruldu)?.Yil);

        var etiketler = new List<(int Kod, string Ad, int Yil, SinirDurumu Insan, string Kaynak)>();
        foreach (var k in kararlar)
        {
            if (k.Karar == DurumKarariTuru.Esleme)
            {
                if (ilkYillar.GetValueOrDefault(k.IliskiliMahalleKoy!.Kod) is { } yil)
                    etiketler.Add((k.MahalleKoy.Kod, k.MahalleKoy.Ad, yil, SinirDurumu.SiniriDegisti, $"#{k.Id} eşleme → {k.IliskiliMahalleKoy.Ad}"));
            }
            else
            {
                etiketler.Add((k.MahalleKoy.Kod, k.MahalleKoy.Ad, k.Secim!.Tarih.Year,
                    k.Karar == DurumKarariTuru.Sabit ? SinirDurumu.Sabit : SinirDurumu.SiniriDegisti, $"#{k.Id}"));
            }
        }

        Console.WriteLine($"EŞİK DOĞRULAMA · {ilce.Ad} · eşikler: {MahalleKomutlari.EsikMetni(esikler)}");
        Console.WriteLine($"Onaylı, geçerli insan kararı: {kararlar.Count} ({etiketler.Count} mahalle-seçim etiketi).");
        if (etiketler.Count == 0)
        {
            Console.WriteLine("Karşılaştırılacak insan kararı yok. Karar girmek için: karar-talep ... ve karar-onayla ...");
            return 0;
        }

        var satirlar = new List<(int Kod, string Ad, int Yil, SinirDurumu Insan, SinirDurumu? Otomatik, decimal? Degisim, string Kaynak)>();
        foreach (var e in etiketler)
        {
            var d = sinir.Birim(e.Kod)?.Yillar.FirstOrDefault(y => y.Yil == e.Yil);
            satirlar.Add((e.Kod, e.Ad, e.Yil, e.Insan, d?.Otomatik, d?.PayDegisimi, e.Kaynak));
        }

        // Karışıklık tablosu: satır insan kararı, sütun otomatik tespit.
        SinirDurumu[] otoSutunlar = [SinirDurumu.Sabit, SinirDurumu.Supheli, SinirDurumu.SiniriDegisti];
        Console.WriteLine();
        Tablo.Yaz(
            ["İnsan kararı \\ otomatik", "sabit", "şüpheli", "sınırı değişti", "diğer"],
            new[] { SinirDurumu.Sabit, SinirDurumu.SiniriDegisti }.Select(i => (IReadOnlyList<string>)new List<string>(
                [MahalleKomutlari.DurumAdi(i), .. otoSutunlar.Select(o => satirlar.Count(s => s.Insan == i && s.Otomatik == o).ToString(CultureInfo.InvariantCulture)),
                 satirlar.Count(s => s.Insan == i && (s.Otomatik is null || !otoSutunlar.Contains(s.Otomatik.Value))).ToString(CultureInfo.InvariantCulture)])).ToList(),
            sagaYasli: new HashSet<int> { 1, 2, 3, 4 });

        int uyusan = satirlar.Count(s => s.Otomatik == s.Insan);
        int supheli = satirlar.Count(s => s.Otomatik == SinirDurumu.Supheli);
        int celisen = satirlar.Count - uyusan - supheli;
        Console.WriteLine();
        Console.WriteLine(string.Create(Turkce, $"Örtüşme: {uyusan}/{satirlar.Count} (%{100.0 * uyusan / satirlar.Count:0}); şüpheli (insan kararı gerekti): {supheli}; çelişen: {celisen}."));

        var sabitler = satirlar.Where(s => s.Insan == SinirDurumu.Sabit && s.Degisim is not null).Select(s => s.Degisim!.Value).ToList();
        var degisenler = satirlar.Where(s => s.Insan == SinirDurumu.SiniriDegisti && s.Degisim is not null).Select(s => s.Degisim!.Value).ToList();
        if (sabitler.Count > 0 && degisenler.Count(d => d < 0) > 0)
            Console.WriteLine(string.Create(Turkce,
                $"İnsan kararlarına uyan düşüş eşiği aralığı: sabitlerin en büyük düşüşü %{-Math.Min(0, sabitler.Min()) * 100:0.0} < eşik ≤ değişenlerin en küçük düşüşü %{-degisenler.Where(d => d < 0).Max() * 100:0.0}."));

        Console.WriteLine();
        Tablo.Yaz(["Kod", "Mahalle", "Seçim", "İnsan", "Otomatik", "Pay değişimi", "Karar", "Sonuç"],
            satirlar.OrderBy(s => s.Degisim).Select(s => (IReadOnlyList<string>)
            [
                s.Kod.ToString(CultureInfo.InvariantCulture), s.Ad, s.Yil.ToString(CultureInfo.InvariantCulture),
                MahalleKomutlari.DurumAdi(s.Insan), s.Otomatik is { } o ? MahalleKomutlari.DurumAdi(o) : "—",
                s.Degisim is { } d ? string.Create(Turkce, $"{d * 100:+0.0;-0.0}%") : "—", s.Kaynak,
                s.Otomatik == s.Insan ? "uyuştu" : s.Otomatik == SinirDurumu.Supheli ? "şüpheli" : "ÇELİŞTİ",
            ]).ToList(),
            sagaYasli: new HashSet<int> { 0, 2, 5 });
        return 0;
    }

    // ================= csv: UTF-8 BOM, ';' ayraç, ondalıkta virgül (Excel tr-TR) =================

    private static string Csv(decimal? d) => d?.ToString("0.######", Turkce) ?? "";
    private static string Csv(long? d) => d?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string Csv(string? s) => (s ?? "").Replace(';', ',');
    private static string Csv(int d) => d.ToString(CultureInfo.InvariantCulture);

    private static string CsvYaz(string yol, IEnumerable<string[]> satirlar)
    {
        using var yazici = new StreamWriter(yol, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        foreach (var s in satirlar)
            yazici.WriteLine(string.Join(';', s));
        return yol;
    }

    private static string ErimeCsv(string yol, ErimeRaporu r, SinirAnaliziSonucu sinir)
    {
        string[] baslik = ["sira", "birim_kod", "birim_adi", "donem", "taban_yili", "taban_orani", "taban_toplam_oy", "taban_dsp_oyu",
            "son_yil", "son_orani", "son_toplam_oy", "son_dsp_oyu", "puan_farki", "korunma_orani", "beklenen_oy", "kayip_oy", "kale", "sinir_uyarisi", "not"];
        return CsvYaz(yol, r.Satirlar.Select((s, i) => new[]
        {
            Csv(i + 1), Csv(s.BirimKod), Csv(s.BirimAdi), Csv(sinir.Birim(s.BirimKod)?.Donem), Csv(s.TabanYili),
            Csv(s.TabanOrani), Csv(s.TabanToplamOy), Csv(s.TabanDspOyu),
            r.SonYil?.ToString(CultureInfo.InvariantCulture) ?? "", Csv(s.SonOrani), Csv(s.SonToplamOy), Csv(s.SonDspOyu),
            Csv(s.PuanFarki), Csv(s.KorunmaOrani), Csv(s.BeklenenOy), Csv(s.KayipOy), s.KaleMi ? "evet" : "hayir",
            Csv(SinirUyarisi(sinir.Birim(s.BirimKod))), Csv(s.Not),
        }).Prepend(baslik));
    }

    private static string KaleCsv(string yol, Dictionary<OyTuru, ErimeRaporu> raporlar)
    {
        string[] baslik = ["seri", "yil", "birim_sayisi", "toplam_oy", "dsp_oyu", "oran"];
        return CsvYaz(yol, raporlar.SelectMany(kv => kv.Value.KaleOrtalamalari.Select(k => new[]
        {
            OyTuruAdlari.Ad(kv.Key), Csv(k.Yil), Csv(k.BirimSayisi), Csv(k.ToplamOy), Csv(k.DspOyu), Csv(k.Oran),
        })).Prepend(baslik));
    }

    private static string IlceCsv(string yol, Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>> ilce)
    {
        string[] baslik = ["seri", "yil", "toplam_oy", "dsp_oyu", "oran", "puan_farki_2009", "korunma_orani", "beklenen_oy", "kayip_oy"];
        return CsvYaz(yol, ilce.SelectMany(kv => kv.Value.Select(s => new[]
        {
            OyTuruAdlari.Ad(kv.Key), Csv(s.Yil), Csv(s.ToplamOy), Csv(s.DspOyu), Csv(s.Oran), Csv(s.PuanFarki), Csv(s.KorunmaOrani), Csv(s.BeklenenOy), Csv(s.KayipOy),
        })).Prepend(baslik));
    }

    private static string SinirCsv(string yol, SinirAnaliziSonucu sinir)
    {
        string[] baslik = ["birim_kod", "birim_adi", "yil", "otomatik", "sonuc", "dogrulandi", "pay", "pay_degisimi", "karar", "donem"];
        return CsvYaz(yol, sinir.Birimler.SelectMany(b => b.Yillar.Select(y => new[]
        {
            Csv(b.BirimKod), Csv(b.BirimAdi), Csv(y.Yil), y.Otomatik.ToString(), y.Sonuc.ToString(), y.Dogrulandi ? "evet" : "hayir",
            Csv(y.Pay), Csv(y.PayDegisimi), Csv(y.Karar), b.Donem,
        })).Prepend(baslik));
    }
}
