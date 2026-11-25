using SecimSistemi.Domain.Analiz;
using Soz = SecimSistemi.Web.Raporlar.SutunSozlugu;

namespace SecimSistemi.Web.Raporlar;

/// <summary>Mahalle grupları, öncelik listesi ve meclis hesaplayıcısının Excel/PDF belgeleri (sayfalarla aynı hazırlayıcılardan).</summary>
public static partial class RaporBelgeleri
{
    // ---------------- Mahalle grupları ----------------

    public static RaporBelgesi Gruplar(GrupSonuclari s, KaynakListesi kaynaklar, DateTime uretim)
    {
        var ilce = s.Ilce ?? throw new ArgumentException("İlçe yok.", nameof(s));
        var g = s.Gruplar ?? throw new ArgumentException("Grup yok.", nameof(s));
        var bolumler = new List<TabloBolumu>();

        Sutun[] ozetSutunlari = [new(Soz.Grup), new("Tanım"), new("Mahalle", HucreTuru.Tamsayi), new("Son toplam oy", HucreTuru.Tamsayi)];
        var ozet = Enum.GetValues<MahalleGrubu>().Select(x => new RaporSatiri(
            [Metinler.Ad(x), Metinler.Tanim(x), g.Sayi(x), g.Satirlar.Where(m => m.Grup == x).Sum(m => m.SonToplamOy ?? 0)])).ToList();
        bolumler.Add(new TabloBolumu("Gruplar", "Gruplar",
            $"Bu eşikle ({GrupAyarlari.Metin(g.Esikler)}) {g.DegismeyenSayisi} mahalle \"değişmedi\" sayıldı." +
            (g.Satirlar.Count(m => m.Grup is null) is > 0 and var grupsuz ? $" {grupsuz} mahallenin grubu hesaplanamadı." : ""),
            ozetSutunlari, ozet, []));

        Sutun[] kareSutunlari = [new("Kare"), new(Soz.BugunkuGuc), new(Soz.Egilim), new("Mahalle", HucreTuru.Tamsayi), new("Son toplam oy", HucreTuru.Tamsayi), new("Mahalleler")];
        var kareler = s.Kareler().Select(k => new RaporSatiri(
        [
            k.Ad, k.Guclu ? "ilçenin üstünde" : "ilçenin altında", k.Yukseldi ? "korudu ya da yükseldi (puan farkı ≥ 0)" : "geriledi (puan farkı < 0)",
            k.Sayi, k.ToplamOy, string.Join(", ", k.Mahalleler.Select(m => m.BirimAdi)),
        ])).ToList();
        bolumler.Add(new TabloBolumu("Bugünkü güç × eğilim", "2x2",
            "Bugünkü güç: son oran ilçenin son oranının üstünde mi. Eğilim: dönem içi puan farkı. Gruplar ise dönem başındaki güce göredir.",
            kareSutunlari, kareler, [], RaporGrafikleri.GucEgilim(s) is { } dg ? [dg] : null));

        Sutun[] sutunlar =
        [
            new(Soz.Kod, HucreTuru.Kod), new(Soz.Mahalle), new(Soz.Donem), new(Soz.Grup), new(Soz.DonemBasindakiGuc, HucreTuru.Puan),
            new(Soz.BugunkuGuc, HucreTuru.Puan), new("Puan farkı", HucreTuru.Puan, Soz.PuanTabana), new(Soz.OyDegisimi, HucreTuru.Tamsayi),
            new($"{Soz.GoreliDegisim.Ad} %", HucreTuru.Puan, Soz.GoreliDegisim), new(Soz.Egilim), new(Soz.Uyari),
        ];
        var satirlar = g.Satirlar.Select(m =>
        {
            var uyari = s.Erime.Uyari(m.BirimKod);
            return new RaporSatiri(
            [
                m.BirimKod, m.BirimAdi, s.Erime.Sinir?.Birim(m.BirimKod)?.Donem, m.Grup is { } gr ? Metinler.Ad(gr) : m.Not,
                m.DonemBasindakiGuc, m.BugunkuGuc, m.PuanFarki, m.OyDegisimi, m.GoreliDegisim * 100,
                m.Egilim is { } e ? Metinler.Ad(e) : null, uyari,
            ], uyari is null ? SatirTuru.Normal : SatirTuru.Uyari);
        }).ToList();
        bolumler.Add(new TabloBolumu("Mahalleler", "Mahalleler", null, sutunlar, satirlar, []));

        return new RaporBelgesi("Mahalle grupları", s.FiltreOzeti, kaynaklar, uretim, bolumler,
            [.. GrupNotlari, .. OrtakNotlar(s.Erime.Sinir?.Esikler)], $"gruplar_{ilce.Kod}_{Metinler.Kod(s.Erime.OyTuru)}");
    }

    // ---------------- Öncelik listesi ----------------

    public static RaporBelgesi Oncelik(GrupSonuclari s, KaynakListesi kaynaklar, DateTime uretim)
    {
        var ilce = s.Ilce ?? throw new ArgumentException("İlçe yok.", nameof(s));
        Sutun[] sutunlar =
        [
            new("#", HucreTuru.Kod), new(Soz.Kod, HucreTuru.Kod), new(Soz.Mahalle), new(Soz.Grup), new(Soz.Kayip, HucreTuru.Tamsayi),
            new("Toplam oy", HucreTuru.Tamsayi, Soz.SonToplam), new(Soz.Buyukluk, HucreTuru.Kod), new(Soz.Neden),
        ];
        var bolumler = s.SuzulmusOncelik().Select(l => new TabloBolumu(
            $"Dönem {l.Donem} · {l.Satirlar.Count} mahalle", $"Dönem {l.Donem}", null, sutunlar,
            l.Satirlar.Select(x => new RaporSatiri(
            [
                x.Sira, x.Grup.BirimKod, x.Grup.BirimAdi, x.Grup.Grup is { } g ? Metinler.Ad(g) : null, x.KayipOy, x.Grup.SonToplamOy,
                x.BuyuklukSirasi, OncelikCumlesi.Yaz(x),
            ], x.SupheliYillar.Count > 0 ? SatirTuru.Uyari : SatirTuru.Normal)).ToList(),
            s.Sorgu.GrupSecimi is null ? [] : ["Grup süzgeci açık: # sütunu mahallenin süzülmemiş dönem listesindeki sırasıdır."])).ToList();

        var ek = (s.Sorgu.Grup is { } gk ? $"_{gk}" : "") + (s.Sorgu.Donem is { } d ? $"_{d}" : "");
        return new RaporBelgesi("Öncelik listesi", $"{s.FiltreOzeti} · süzgeç: {s.SuzgecOzeti}", kaynaklar, uretim, bolumler,
            [.. OncelikNotlari, .. OrtakNotlar(s.Erime.Sinir?.Esikler)], $"oncelik_{ilce.Kod}_{Metinler.Kod(s.Erime.OyTuru)}{ek}");
    }

    // ---------------- Meclis hesaplayıcısı ----------------

    public static RaporBelgesi Meclis(MeclisSonuclari m, KaynakListesi kaynaklar, DateTime uretim)
    {
        var ilce = m.Ilce ?? throw new ArgumentException("İlçe yok.", nameof(m));
        var secim = m.Secim ?? throw new ArgumentException("Seçim yok.", nameof(m));
        var mevcut = m.Mevcut ?? throw new ArgumentException("Meclis sonucu yok.", nameof(m));
        var bolumler = new List<TabloBolumu>();

        var senaryo = m.Senaryo;
        Sutun[] sutunlar =
        [
            new(Soz.Liste), new(Soz.MeclisOyu, HucreTuru.Tamsayi), new("%", HucreTuru.Oran, Soz.DspOrani), new(Soz.Baraj),
            new(Soz.KalanOy, HucreTuru.Tamsayi), new(Soz.Bolmeyle, HucreTuru.Kod), new(Soz.KontenjanUye, HucreTuru.Kod), new(Soz.ToplamUye, HucreTuru.Kod),
            .. senaryo is null ? Array.Empty<Sutun>() : [new Sutun($"+{Metinler.Oy(m.EkOy)} oyla üye", HucreTuru.Kod)],
        ];
        var satirlar = mevcut.Listeler.Where(l => l.Toplam > 0 || l.Ad == MeclisSonuclari.Hedef || l.Oran >= 1).Select(l => new RaporSatiri(
        [
            l.Ad, l.Oy, l.Oran, l.BarajiGecti ? "geçti" : null, l.BarajiGecti ? l.KalanOy : null, l.Koltuk, l.Kontenjan, l.Toplam,
            .. senaryo is null ? Array.Empty<object?>() : [senaryo.Koltuk(l.Ad)],
        ], l.Ad == MeclisSonuclari.Hedef ? SatirTuru.Vurgu : SatirTuru.Normal)).ToList();
        var notlar = new List<string>();
        if (mevcut.KuraListeleri.Count > 0)
            notlar.Add($"Son üyelikte eşit paylar var ({string.Join(", ", mevcut.KuraListeleri)}): kanuna göre kura çekilir; hesap üyeliği toplam oyu fazla olana verdi.");
        if (m.Hata is not null) notlar.Add(m.Hata);
        bolumler.Add(new TabloBolumu($"{ilce.Ad} · {secim.Yil} · belediye meclisi", "Dağılım",
            $"{mevcut.UyeSayisi} üye ({mevcut.KontenjanSayisi} kontenjan{(mevcut.KontenjanAlan is { } ka ? $", {ka}" : "")}) · geçerli oy {Metinler.Oy(mevcut.GecerliOy)} · " +
            $"baraj {Metinler.Oy(mevcut.BarajOyu)} oyun üstü. DSP satırı kalın.",
            sutunlar, satirlar, notlar));

        if (m.DspVar)
        {
            var dsp = mevcut.Liste(MeclisSonuclari.Hedef)!;
            Sutun[] esikSutunlari = [new("Hedef"), new(Soz.EkOy, HucreTuru.Tamsayi), new("DSP'nin oyu", HucreTuru.Tamsayi)];
            var esik = new List<RaporSatiri>
            {
                new(["Barajı geçmek", m.BarajIcin, m.BarajIcin is { } b ? dsp.Oy + b : null]),
            };
            esik.AddRange(m.Esikler.Select(e => new RaporSatiri([$"{e.Koltuk} üye", e.EkOy, dsp.Oy + e.EkOy])));
            bolumler.Add(new TabloBolumu("DSP kaç oy daha alsaydı?", "DSP için ek oy",
                $"DSP {Metinler.Oy(dsp.Oy)} oy (%{Metinler.Oran(dsp.Oran)}), {dsp.Toplam} üye. Ek oy " +
                $"{(m.Kaynak == EkOyKaynagi.YeniSecmen ? "yeni seçmenden geliyor (geçerli oy toplamı da artar)" : "en büyük partiden alınıyor (toplam değişmez)")}.",
                esikSutunlari, esik, m.Esikler.Count == 0 ? ["Bu kaynakla bir sonraki üyeliğe ulaşılamıyor."] : []));
        }

        var dosya = $"meclis_{ilce.Kod}_{secim.Yil}_{m.UyeSayisi}" + (m.EkOy > 0 ? $"_ek{m.EkOy}" : "");
        return new RaporBelgesi("Belediye meclisi hesaplayıcı", m.FiltreOzeti, kaynaklar, uretim, bolumler,
            [.. MeclisNotlari, $"Veri kaynağı: {Metinler.VeriKaynagi}."], dosya, m.UyeUyarisi);
    }
}
