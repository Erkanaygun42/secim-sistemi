using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Sayfalardaki raporları <see cref="RaporBelgesi"/>'ne çevirir. Satır sırası ve değerler ekrandakiyle aynıdır;
/// ekrandaki açıklama balonlarının yerine "Uyarı" sütunu kullanılır (dosyada balon yok).
/// </summary>
public static class RaporBelgeleri
{
    // Rapora özel notlar; sayfalar da aynı metinleri gösterir.
    public static readonly string[] KarneNotlari =
    [
        "Toplam oy = parti ve bağımsız aday oylarının toplamı (YSK sandık dosyalarında geçerli oy sütunu yok). Oran yüzde olarak verilir.",
        "\"—\": DSP o seçimde o oy türünde kendi listesiyle pusulada değildi; oy 0 değil, boştur.",
        "Dönem: o seçime göre karşılaştırılabilir dönem (son sınır değişikliğinden o seçime). Sınır durumu belediye meclisi oyundan hesaplanır, bütün oy türlerinde aynıdır.",
    ];

    public static readonly string[] MahalleNotlari =
    [
        "Puan farkı: DSP oranının bir önceki yerel seçime göre farkı, yüzde puan. Sınırın değiştiği seçimde bu fark aynı toprağı göstermez.",
        "\"—\": DSP o seçimde o oy türünde kendi listesiyle pusulada değildi; oy 0 değil, boştur.",
        "Sınır durumu belediye meclisi oyundan hesaplanır, bütün oy türlerinde aynıdır.",
    ];

    public static readonly string[] ErimeNotlari =
    [
        "Korunma = son oran ÷ taban oranı (1 = oran korundu). Beklenen oy = taban oranı × son seçimdeki toplam oy. Kayıp oy = beklenen − gerçek; eksi değer, DSP'nin tabanın üstünde oy aldığını gösterir.",
        "Son seçim: o seride DSP'nin kendi listesiyle pusulada olduğu en son yerel seçim.",
        "Toplam oy = parti ve bağımsız aday oylarının toplamı. Oranlar yüzde, puan farkı yüzde puandır.",
    ];

    /// <summary>Her raporun sonundaki ortak notlar: veri kaynağı, eşikler, eşleme sınırı.</summary>
    public static List<string> OrtakNotlar(SinirEsikleri? esikler)
    {
        var notlar = new List<string> { $"Veri kaynağı: {Metinler.VeriKaynagi}." };
        if (esikler is not null) notlar.Add($"{Metinler.Esikler(esikler)} ✓ = insan kararıyla doğrulandı.");
        notlar.Add(Metinler.EslemeNotu);
        return notlar;
    }

    private static string Donem(int? baslangic, int yil) =>
        baslangic is not { } d ? Metinler.Bos : d == yil ? $"{d}" : $"{d}–{yil}";

    // ---------------- Mahalle karnesi ----------------

    public static RaporBelgesi Karne(KarneSonucu sonuc, IReadOnlyList<string> kaynaklar, DateTime uretim)
    {
        var k = sonuc.Karne ?? throw new ArgumentException("Karne boş.", nameof(sonuc));
        Sutun[] sutunlar =
        [
            new("#", HucreTuru.Kod), new("Kod", HucreTuru.Kod), new("Mahalle/köy"), new("Sandık", HucreTuru.Tamsayi),
            new("Toplam oy", HucreTuru.Tamsayi), new("DSP oyu", HucreTuru.Tamsayi), new("DSP %", HucreTuru.Oran),
            new("Dönem"), new("Sınır durumu"), new("Uyarı"),
        ];
        var satirlar = k.Satirlar.Select((s, i) => new RaporSatiri(
        [
            i + 1, s.BirimKod, s.BirimAdi, s.SandikSayisi, s.ToplamOy, s.DspOyu, s.DspOrani,
            Donem(s.DonemBaslangici, k.Secim.Yil), Metinler.Sinir(s.Sinir), s.Uyari ? Metinler.Uyari(s.Supheliler) : null,
        ], s.Uyari ? SatirTuru.Uyari : SatirTuru.Normal)).ToList();

        var toplamOy = k.Satirlar.Sum(s => s.ToplamOy);
        var pusulada = k.Satirlar.Where(s => s.PusuladaMi).ToList();
        long? dsp = pusulada.Count == 0 ? null : pusulada.Sum(s => s.DspOyu ?? 0);
        satirlar.Add(new RaporSatiri(
        [
            null, null, "İlçe toplamı", k.Satirlar.Sum(s => s.SandikSayisi), toplamOy, dsp,
            dsp is null || toplamOy == 0 ? null : 100m * dsp.Value / toplamOy, null, null, null,
        ], SatirTuru.Toplam));

        var tablo = new TabloBolumu($"{k.Ilce.Ad} · {k.Secim.Yil} · {Metinler.Ad(k.OyTuru)}", "Karne", null, sutunlar, satirlar, []);
        return new RaporBelgesi("Mahalle karnesi", sonuc.FiltreOzeti, kaynaklar, uretim, [tablo],
            [.. KarneNotlari, .. OrtakNotlar(k.Esikler)],
            $"karne_{k.Ilce.Kod}_{k.Secim.Yil}_{Metinler.Kod(k.OyTuru)}");
    }

    // ---------------- Mahalle detayı ----------------

    public static RaporBelgesi Mahalle(MahalleDetayi d, IReadOnlyList<string> kaynaklar, DateTime uretim)
    {
        var bolumler = new List<TabloBolumu>();
        Sutun[] sutunlar =
        [
            new("Seçim", HucreTuru.Kod), new("Sandık", HucreTuru.Tamsayi), new("Toplam oy", HucreTuru.Tamsayi),
            new("DSP oyu", HucreTuru.Tamsayi), new("DSP %", HucreTuru.Oran), new("Puan farkı", HucreTuru.Puan),
            new("Sınır durumu"), new("Uyarı"),
        ];
        foreach (var g in d.Satirlar.GroupBy(x => x.OyTuru).OrderBy(g => g.Key))
        {
            var satirlar = g.OrderBy(x => x.Yil).Select(r =>
            {
                var kesinti = r.Sinir?.Sonuc is SinirDurumu.SiniriDegisti or SinirDurumu.Yeni && r.PuanFarki is not null;
                string? uyari = kesinti ? $"Sınır {r.Yil} seçiminde değişti; {r.OncekiYil} ile karşılaştırma aynı toprağı göstermez."
                    : r.Sinir?.Sonuc == SinirDurumu.Supheli ? Metinler.Uyari([r.Sinir]) : null;
                return new RaporSatiri(
                    [r.Yil, r.SandikSayisi, r.ToplamOy, r.DspOyu, r.DspOrani, r.PuanFarki, Metinler.Sinir(r.Sinir), uyari],
                    r.Sinir?.Sonuc == SinirDurumu.Supheli ? SatirTuru.Uyari : SatirTuru.Normal);
            }).ToList();
            bolumler.Add(new TabloBolumu(Metinler.Ad(g.Key), Metinler.Ad(g.Key), null, sutunlar, satirlar, []));
        }

        if (d.Sinir is { } s)
        {
            Sutun[] sinirSutunlari =
            [
                new("Seçim", HucreTuru.Kod), new("İlçe meclis oyundaki pay %", HucreTuru.Oran), new("Pay değişimi %", HucreTuru.Puan),
                new("Otomatik tespit"), new("Sonuç"), new("İnsan kararı"),
            ];
            var satirlar = s.Yillar.Select(y => new RaporSatiri(
                [y.Yil, y.Pay * 100, y.PayDegisimi * 100, Metinler.Ad(y.Otomatik), Metinler.Sinir(y), y.Karar],
                y.Sonuc == SinirDurumu.Supheli ? SatirTuru.Uyari : SatirTuru.Normal)).ToList();
            bolumler.Add(new TabloBolumu("Sınır geçmişi", "Sınır geçmişi", $"Karşılaştırılabilir dönem: {s.Donem}.", sinirSutunlari, satirlar, []));
        }

        var ozet = $"{d.Ilce.IlAdi} / {d.Ilce.Ad} · {d.BirimAdi} (kod {d.BirimKod})" + (d.Sinir is { } sd ? $" · karşılaştırılabilir dönem {sd.Donem}" : "");
        return new RaporBelgesi($"Mahalle detayı · {d.BirimAdi}", ozet, kaynaklar, uretim, bolumler,
            [.. MahalleNotlari, .. OrtakNotlar(d.Esikler)], $"mahalle_{d.Ilce.Kod}_{d.BirimKod}");
    }

    // ---------------- Erime raporu ----------------

    public static RaporBelgesi Erime(ErimeSonucu e, IReadOnlyList<string> kaynaklar, DateTime uretim)
    {
        var ilce = e.Ilce ?? throw new ArgumentException("İlçe yok.", nameof(e));
        var bolumler = new List<TabloBolumu>();

        Sutun[] ilceSutunlari =
        [
            new("Seri"), new("Yıl", HucreTuru.Kod), new("Toplam oy", HucreTuru.Tamsayi), new("DSP oyu", HucreTuru.Tamsayi),
            new("DSP %", HucreTuru.Oran), new($"Puan ({ErimeHesabi.TabanYili}'a göre)", HucreTuru.Puan), new("Korunma", HucreTuru.Katsayi),
            new("Beklenen oy", HucreTuru.Tamsayi), new("Kayıp oy", HucreTuru.Tamsayi),
        ];
        var ilceSatirlari = e.IlceErimesi.SelectMany(kv => kv.Value.Select(s => new RaporSatiri(
            [Metinler.Ad(kv.Key), s.Yil, s.ToplamOy, s.DspOyu, s.Oran, s.PuanFarki, s.KorunmaOrani, s.BeklenenOy, s.KayipOy]))).ToList();
        bolumler.Add(new TabloBolumu($"İlçe düzeyi · {ErimeHesabi.TabanYili}'dan bugüne", "İlçe düzeyi",
            $"{ilce.Ad} ilçesinin tamamı. İlçe sınırı bu dönemde değişmedi.", ilceSutunlari, ilceSatirlari, []));

        if (e.Rapor is { } r)
        {
            Sutun[] sutunlar =
            [
                new("#", HucreTuru.Kod), new("Kod", HucreTuru.Kod), new("Mahalle/köy"), new("Dönem"),
                new("Taban %", HucreTuru.Oran), new("Son %", HucreTuru.Oran), new("Puan", HucreTuru.Puan), new("Korunma", HucreTuru.Katsayi),
                new("Taban DSP", HucreTuru.Tamsayi), new("Son DSP", HucreTuru.Tamsayi), new("Son toplam", HucreTuru.Tamsayi),
                new("Beklenen", HucreTuru.Tamsayi), new("Kayıp", HucreTuru.Tamsayi), new("Kale"), new("Uyarı"),
            ];
            var satirlar = new List<RaporSatiri>();
            var sira = 0;
            foreach (var g in e.DonemGruplari())
            {
                satirlar.Add(new RaporSatiri([$"Dönem {g.Key} · {g.Count()} birim"], SatirTuru.Grup));
                foreach (var s in g)
                {
                    var uyari = e.Uyari(s.BirimKod);
                    satirlar.Add(new RaporSatiri(
                    [
                        ++sira, s.BirimKod, s.BirimAdi, g.Key, s.TabanOrani, s.SonOrani, s.PuanFarki, s.KorunmaOrani,
                        s.TabanDspOyu, s.SonDspOyu, s.SonToplamOy, s.BeklenenOy, s.KayipOy, s.KaleMi ? "kale" : null, uyari,
                    ], uyari is null ? SatirTuru.Normal : SatirTuru.Uyari));
                }
            }
            var notlar = r.Satirlar.Where(s => s.Not is not null).GroupBy(s => s.Not!).Select(g => $"Not ({g.Count()} birim): {g.Key}").ToList();
            bolumler.Add(new TabloBolumu($"Mahalle düzeyi · {Metinler.Ad(e.OyTuru)} · son seçim {r.SonYil?.ToString() ?? Metinler.Bos}",
                "Mahalle düzeyi",
                "Her birimin tabanı kendi karşılaştırılabilir döneminin ilk seçimidir. Sıra: dönem (en uzun önce), sonra kayıp oy (en büyük önce).",
                sutunlar, satirlar, notlar));
        }

        var yillar = e.Raporlar.Values.SelectMany(x => x.KaleOrtalamalari.Select(k => k.Yil)).Distinct().Order().ToList();
        Sutun[] kaleSutunlari = [new("Seri"), .. yillar.Select(y => new Sutun(y.ToString(Metinler.Turkce), HucreTuru.Oran))];
        var kaleSatirlari = e.Raporlar.Select(kv => new RaporSatiri(
            [Metinler.Ad(kv.Key), .. yillar.Select(y => (object?)kv.Value.KaleOrtalamalari.FirstOrDefault(k => k.Yil == y)?.Oran)])).ToList();
        bolumler.Add(new TabloBolumu($"{ErimeHesabi.TabanYili} kaleleri", "Kaleler",
            $"{e.KaleOzeti} · Grubun ağırlıklı oranı (grubun DSP oyu ÷ grubun toplam oyu), yüzde.", kaleSutunlari, kaleSatirlari, []));

        return new RaporBelgesi("Erime raporu", e.FiltreOzeti, kaynaklar, uretim, bolumler,
            [.. ErimeNotlari, .. OrtakNotlar(e.Sinir?.Esikler)], $"erime_{ilce.Kod}_{Metinler.Kod(e.OyTuru)}");
    }
}
