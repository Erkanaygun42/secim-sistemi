using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain.Analiz;
using Soz = SecimSistemi.Web.Raporlar.SutunSozlugu;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Sayfalardaki raporları <see cref="RaporBelgesi"/>'ne çevirir. Satır sırası ve değerler ekrandakiyle aynıdır;
/// ekrandaki açıklama balonlarının yerine "Uyarı" sütunu kullanılır (dosyada balon yok). Sütun açıklamaları
/// <see cref="SutunSozlugu"/>'nden gelir ve PDF'te "Bu rapor nasıl okunur" bölümünü oluşturur.
/// </summary>
public static class RaporBelgeleri
{
    // Rapora özel genel notlar (sütun açıklamaları sözlükte); sayfalar da aynı metinleri gösterir.
    public static readonly string[] KarneNotlari =
    [
        "\"—\": DSP o seçimde o oy türünde kendi listesiyle pusulada değildi; oy 0 değil, boştur.",
        "Sınır durumu belediye meclisi oyundan hesaplanır, bütün oy türlerinde aynıdır.",
    ];

    public static readonly string[] MahalleNotlari =
    [
        "\"—\": DSP o seçimde o oy türünde kendi listesiyle pusulada değildi; oy 0 değil, boştur.",
        "Sınır durumu belediye meclisi oyundan hesaplanır, bütün oy türlerinde aynıdır.",
    ];

    public static readonly string[] ErimeNotlari =
    [
        "Taban: her mahallenin kendi karşılaştırılabilir döneminin ilk seçimi. İlçe tablosunda taban 2009'dur.",
        "Son seçim: o seride DSP'nin kendi listesiyle pusulada olduğu en son yerel seçim.",
    ];

    public static readonly string[] GrupNotlari =
    [
        "Gruplar her mahallenin kendi karşılaştırılabilir dönemine göredir (taban = dönemin ilk seçimi). Güçlü = taban oranı, ilçenin aynı yıldaki oranına eşit ya da üstünde.",
        "Değişim eşiği tek ayarda: appsettings.json → Gruplama (oy farkı ve oranın göreli değişimi; ikisi de aşılmazsa \"değişmedi\").",
        "2x2 tablo ve grafik bugünkü güce göredir, gruplar dönem başındaki güce göre; ikisi bilerek farklıdır.",
    ];

    public static readonly string[] OncelikNotlari =
    [
        "Her karşılaştırılabilir dönem ayrı listededir: farklı tabanlardan gelen kayıplar tek sıralamada karşılaştırılamaz.",
        "Sıra: kayıp oy (beklenen − gerçek) büyükten küçüğe; eşitlikte son seçimdeki toplam oy. Büyüklük sırası ilçenin tamamına göredir.",
        "Cümleler satırdaki sayılardan sabit kalıpla üretilir.",
    ];

    public static readonly string[] MeclisNotlari =
    [
        "Dağıtım 2972 sayılı Kanun'a göredir: geçerli oyun onda biri her listenin oyundan çıkarılır, geriye oyu kalmayan üye alamaz; " +
        "üyelikler kalan oyların 1, 2, 3 … diye bölünmesiyle dağıtılır (m.23/a). Bu yüzden barajı az farkla geçen liste üye alamayabilir.",
        "Kontenjan üyeliklerinin hepsi geçerli oyların en çoğunu alan partiye gider (m.23/a); sayısı üye sayısına göredir (m.10/c).",
        "Geçerli oy = YSK sandık dosyalarındaki parti ve bağımsız oylarının toplamı. Bağımsız aday en çok bir üyelik alır (varsayım).",
        "Meclis üye sayısı nüfusa bağlıdır (m.5/b) ve tanım dosyasından okunur (Tanimlar/meclis-uye-sayilari.csv). Doğrulanmamış sayı sayfada uyarılır.",
        "Ek oy \"yeni seçmenden\" gelirse geçerli oy toplamı da artar (baraj yükselir); \"en büyük partiden\" gelirse toplam değişmez.",
        "Ayrıntı ve varsayımlar: docs/kararlar.md K36.",
    ];

    /// <summary>Her raporun sonundaki ortak notlar: veri kaynağı, eşikler, eşleme sınırı.</summary>
    public static List<string> OrtakNotlar(SinirEsikleri? esikler)
    {
        var notlar = new List<string> { $"Veri kaynağı: {Metinler.VeriKaynagi}." };
        if (esikler is not null) notlar.Add($"{Metinler.Esikler(esikler)} ✓ = insan kararıyla doğrulandı.");
        notlar.Add(Metinler.EslemeNotu);
        return notlar;
    }

    /// <summary>Karnede puan farkı sütununun başlığı: satırların çoğunun karşılaştırıldığı önceki seçime göre.</summary>
    public static string PuanFarkiBasligi(Karne k) =>
        k.Satirlar.Where(s => s.OncekiYil is not null).GroupBy(s => s.OncekiYil!.Value).MaxBy(g => g.Count())?.Key is { } y
            ? $"Puan farkı ({Metinler.Yonelme(y)} göre)"
            : "Puan farkı (önceki seçime göre)";

    /// <summary>Puan farkının uyarısı: sınır tam bu seçimde değiştiyse.</summary>
    public static string? PuanUyarisi(int yil, int? oncekiYil, bool kesintili) =>
        kesintili ? $"Sınır {yil} seçiminde değişti; {oncekiYil} ile puan farkı aynı toprağı göstermez." : null;

    // ---------------- Mahalle karnesi ----------------

    public static RaporBelgesi Karne(KarneSonucu sonuc, KaynakListesi kaynaklar, DateTime uretim)
    {
        var k = sonuc.Karne ?? throw new ArgumentException("Karne boş.", nameof(sonuc));
        Sutun[] sutunlar =
        [
            new("#", HucreTuru.Kod), new(Soz.Kod, HucreTuru.Kod), new(Soz.Mahalle), new(Soz.Sandik, HucreTuru.Tamsayi),
            new(Soz.ToplamOy, HucreTuru.Tamsayi), new(Soz.DspOyu, HucreTuru.Tamsayi), new(Soz.DspOrani, HucreTuru.Oran),
            new(PuanFarkiBasligi(k), HucreTuru.Puan, Soz.PuanFarki),
            new(Soz.Donem), new(Soz.Sinir), new(Soz.Uyari),
        ];
        var satirlar = k.Satirlar.Select((s, i) => new RaporSatiri(
        [
            i + 1, s.BirimKod, s.BirimAdi, s.SandikSayisi, s.ToplamOy, s.DspOyu, s.DspOrani, s.PuanFarki,
            s.DonemSiniri?.Donem, Metinler.Sinir(s.DonemSiniri),
            s.Uyari ? Metinler.Uyari(s.Supheliler) : PuanUyarisi(k.Secim.Yil, s.OncekiYil, s.PuanKesintili),
        ], s.Uyari ? SatirTuru.Uyari : SatirTuru.Normal)).ToList();

        var toplamOy = k.Satirlar.Sum(s => s.ToplamOy);
        var pusulada = k.Satirlar.Where(s => s.PusuladaMi).ToList();
        long? dsp = pusulada.Count == 0 ? null : pusulada.Sum(s => s.DspOyu ?? 0);
        satirlar.Add(new RaporSatiri(
        [
            null, null, "İlçe toplamı", k.Satirlar.Sum(s => s.SandikSayisi), toplamOy, dsp,
            dsp is null || toplamOy == 0 ? null : 100m * dsp.Value / toplamOy, null, null, null, null,
        ], SatirTuru.Toplam));

        var tablo = new TabloBolumu($"{k.Ilce.Ad} · {k.Secim.Yil} · {Metinler.Ad(k.OyTuru)}", "Karne", null, sutunlar, satirlar, []);
        return new RaporBelgesi("Mahalle karnesi", sonuc.FiltreOzeti, kaynaklar, uretim, [tablo],
            [.. KarneNotlari, .. OrtakNotlar(k.Esikler)],
            $"karne_{k.Ilce.Kod}_{k.Secim.Yil}_{Metinler.Kod(k.OyTuru)}");
    }

    // ---------------- Mahalle detayı ----------------

    public static RaporBelgesi Mahalle(MahalleDetayi d, KaynakListesi kaynaklar, DateTime uretim)
    {
        var bolumler = new List<TabloBolumu>();

        // Özet: yıllara göre oran (grafik + verisi), oy türleri yan yana.
        var turler = d.Satirlar.Select(s => s.OyTuru).Distinct().OrderBy(Metinler.SeriSirasi).ToList();
        var yillar = d.Satirlar.Select(s => s.Yil).Distinct().Order().ToList();
        Sutun[] ozetSutunlari =
        [
            new(Soz.Yil, HucreTuru.Kod), .. turler.Select(t => new Sutun($"{Metinler.Ad(t)} %", HucreTuru.Oran, Soz.DspOrani)),
            new(Soz.Donem),
        ];
        var ozetSatirlari = yillar.Select(y =>
        {
            var disarda = d.Sinir is { } s && y < s.DonemBaslangici;
            return new RaporSatiri(
            [
                y, .. turler.Select(t => (object?)d.Satirlar.FirstOrDefault(x => x.Yil == y && x.OyTuru == t)?.DspOrani),
                disarda ? "dönem dışı" : d.Sinir?.DonemSiniri(y)?.Donem,
            ]);
        }).ToList();
        bolumler.Add(new TabloBolumu("Yıllara göre DSP oranı", "Özet", null, ozetSutunlari, ozetSatirlari, [],
            RaporGrafikleri.MahalleOrani(d) is { } g ? [g] : null));

        Sutun[] sutunlar =
        [
            new("Seçim", HucreTuru.Kod, Soz.Yil), new(Soz.Sandik, HucreTuru.Tamsayi), new(Soz.ToplamOy, HucreTuru.Tamsayi),
            new(Soz.DspOyu, HucreTuru.Tamsayi), new(Soz.DspOrani, HucreTuru.Oran), new(Soz.PuanFarki, HucreTuru.Puan),
            new(Soz.Sinir), new(Soz.Uyari),
        ];
        foreach (var grup in d.Satirlar.GroupBy(x => x.OyTuru).OrderBy(g => Metinler.SeriSirasi(g.Key)))
        {
            var satirlar = grup.OrderBy(x => x.Yil).Select(r =>
            {
                var uyari = r.DonemSiniri is { UyariVar: true } ds ? Metinler.Uyari(ds.Ilgili) : PuanUyarisi(r.Yil, r.OncekiYil, r.PuanKesintili);
                return new RaporSatiri(
                    [r.Yil, r.SandikSayisi, r.ToplamOy, r.DspOyu, r.DspOrani, r.PuanFarki, Metinler.Sinir(r.DonemSiniri), uyari],
                    r.DonemSiniri?.UyariVar == true ? SatirTuru.Uyari : SatirTuru.Normal);
            }).ToList();
            bolumler.Add(new TabloBolumu(Metinler.Ad(grup.Key), Metinler.Ad(grup.Key), null, sutunlar, satirlar, []));
        }

        if (d.Sinir is { } sg)
        {
            Sutun[] sinirSutunlari =
            [
                new("Seçim", HucreTuru.Kod, Soz.Yil), new($"{Soz.Pay.Ad} %", HucreTuru.Oran, Soz.Pay), new($"{Soz.PayDegisimi.Ad} %", HucreTuru.Puan, Soz.PayDegisimi),
                new(Soz.OtomatikTespit), new(Soz.YillikSonuc), new(Soz.InsanKarari),
            ];
            var satirlar = sg.Yillar.Select(y => new RaporSatiri(
                [y.Yil, y.Pay * 100, y.PayDegisimi * 100, Metinler.Ad(y.Otomatik), Metinler.Sinir(y), y.Karar],
                y.Sonuc == SinirDurumu.Supheli ? SatirTuru.Uyari : SatirTuru.Normal)).ToList();
            bolumler.Add(new TabloBolumu("Sınır geçmişi", "Sınır geçmişi",
                $"Karşılaştırılabilir dönem: {sg.Donem}. Bu tabloda her satır yalnızca o seçimin tespitidir.", sinirSutunlari, satirlar, []));
        }

        var ozet = $"{d.Ilce.IlAdi} / {d.Ilce.Ad} · {d.BirimAdi} (kod {d.BirimKod})" + (d.Sinir is { } sd ? $" · karşılaştırılabilir dönem {sd.Donem}" : "");
        return new RaporBelgesi($"Mahalle detayı · {d.BirimAdi}", ozet, kaynaklar, uretim, bolumler,
            [.. MahalleNotlari, .. OrtakNotlar(d.Esikler)], $"mahalle_{d.Ilce.Kod}_{d.BirimKod}");
    }

    // ---------------- Erime raporu ----------------

    /// <summary>Mahalle tablosundaki yıl sütunları: serideki seçim yılları.</summary>
    public static IReadOnlyList<int> MahalleYillari(ErimeSonucu e) => e.Rapor?.Yillar ?? [];

    public static RaporBelgesi Erime(ErimeSonucu e, KaynakListesi kaynaklar, DateTime uretim)
    {
        var ilce = e.Ilce ?? throw new ArgumentException("İlçe yok.", nameof(e));
        var bolumler = new List<TabloBolumu>();

        Sutun[] ilceSutunlari =
        [
            new(Soz.Seri), new(Soz.Yil, HucreTuru.Kod), new($"{Soz.ToplamOy.Ad}", HucreTuru.Tamsayi, Soz.ToplamOy), new(Soz.DspOyu, HucreTuru.Tamsayi),
            new(Soz.DspOrani, HucreTuru.Oran), new($"Puan ({Metinler.Yonelme(ErimeHesabi.TabanYili)} göre)", HucreTuru.Puan, Soz.PuanTabana),
            new(Soz.Korunma, HucreTuru.Katsayi), new("Beklenen oy", HucreTuru.Tamsayi, Soz.Beklenen), new("Kayıp oy", HucreTuru.Tamsayi, Soz.Kayip),
        ];
        var ilceSatirlari = e.IlceErimesi.OrderBy(kv => Metinler.SeriSirasi(kv.Key)).SelectMany(kv => kv.Value.Select(s => new RaporSatiri(
            [Metinler.Ad(kv.Key), s.Yil, s.ToplamOy, s.DspOyu, s.Oran, s.PuanFarki, s.KorunmaOrani, s.BeklenenOy, s.KayipOy]))).ToList();
        bolumler.Add(new TabloBolumu($"İlçe düzeyi · {ErimeHesabi.TabanYili}'dan bugüne", "İlçe düzeyi",
            $"{ilce.Ad} ilçesinin tamamı. İlçe sınırı bu dönemde değişmedi.", ilceSutunlari, ilceSatirlari, [],
            RaporGrafikleri.IlceOrani(e) is { } ig ? [ig] : null));

        if (e.Rapor is { } r)
        {
            var yillar = MahalleYillari(e);
            Sutun[] sutunlar =
            [
                new("#", HucreTuru.Kod), new(Soz.Kod, HucreTuru.Kod), new(Soz.Mahalle), new(Soz.Donem),
                .. yillar.Select(y => new Sutun($"{y} %", HucreTuru.Oran, Soz.YilOrani)),
                new(Soz.PuanTabana, HucreTuru.Puan), new(Soz.Korunma, HucreTuru.Katsayi),
                new(Soz.TabanDsp, HucreTuru.Tamsayi), new(Soz.SonDsp, HucreTuru.Tamsayi), new(Soz.SonToplam, HucreTuru.Tamsayi),
                new(Soz.Beklenen, HucreTuru.Tamsayi), new(Soz.Kayip, HucreTuru.Tamsayi), new(Soz.Kale), new(Soz.Uyari),
            ];
            var satirlar = new List<RaporSatiri>();
            var sira = 0;
            foreach (var g in e.DonemGruplari())
            {
                satirlar.Add(new RaporSatiri([$"Dönem {g.Key} · {g.Count()} birim"], SatirTuru.Grup));
                foreach (var s in g)
                {
                    var uyari = e.Uyari(s.BirimKod);
                    var disarda = yillar.Select((y, i) => (y, i)).Where(x => s.DonemDisi(x.y)).Select(x => 4 + x.i).ToHashSet();
                    satirlar.Add(new RaporSatiri(
                    [
                        ++sira, s.BirimKod, s.BirimAdi, g.Key,
                        .. yillar.Select(y => s.DonemDisi(y) ? null : (object?)s.YillikOranlar.GetValueOrDefault(y)),
                        s.PuanFarki, s.KorunmaOrani, s.TabanDspOyu, s.SonDspOyu, s.SonToplamOy, s.BeklenenOy, s.KayipOy,
                        s.KaleMi ? "evet" : null, uyari,
                    ], uyari is null ? SatirTuru.Normal : SatirTuru.Uyari, disarda.Count == 0 ? null : disarda));
                }
            }
            var notlar = r.Satirlar.Where(s => s.Not is not null).GroupBy(s => s.Not!).Select(g => $"Not ({g.Count()} birim): {g.Key}").ToList();
            bolumler.Add(new TabloBolumu($"Mahalle düzeyi · {Metinler.Ad(e.OyTuru)} · son seçim {r.SonYil?.ToString() ?? Metinler.Bos}",
                "Mahalle düzeyi",
                "Her birimin tabanı kendi karşılaştırılabilir döneminin ilk seçimidir. Sıra: dönem (en uzun önce), sonra kayıp oy (en büyük önce). " +
                "Gri ve boş hücre: o yıl mahallenin karşılaştırılabilir döneminin dışında.",
                sutunlar, satirlar, notlar,
                RaporGrafikleri.EnCokKaybedenler(e)));
        }

        var kaleYillari = e.Raporlar.Values.SelectMany(x => x.KaleOrtalamalari.Select(k => k.Yil)).Distinct().Order().ToList();
        Sutun[] kaleSutunlari = [new(Soz.Seri), .. kaleYillari.Select(y => new Sutun($"{y} %", HucreTuru.Oran, Soz.KaleOrani))];
        var kaleSatirlari = e.Raporlar.OrderBy(kv => Metinler.SeriSirasi(kv.Key)).Select(kv => new RaporSatiri(
            [Metinler.Ad(kv.Key), .. kaleYillari.Select(y => (object?)kv.Value.KaleOrtalamalari.FirstOrDefault(k => k.Yil == y)?.Oran)])).ToList();
        bolumler.Add(new TabloBolumu($"{ErimeHesabi.TabanYili} ilk %20", "2009 ilk %20",
            $"{e.KaleOzeti}. Grubun ağırlıklı oranı (grubun DSP oyu ÷ grubun toplam oyu), yüzde.", kaleSutunlari, kaleSatirlari, []));

        return new RaporBelgesi("Erime raporu", e.FiltreOzeti, kaynaklar, uretim, bolumler,
            [.. ErimeNotlari, .. OrtakNotlar(e.Sinir?.Esikler)], $"erime_{ilce.Kod}_{Metinler.Kod(e.OyTuru)}");
    }
}
