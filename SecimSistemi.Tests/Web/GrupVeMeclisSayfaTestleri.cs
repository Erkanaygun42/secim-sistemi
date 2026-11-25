using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Gruplar, öncelik ve meclis sayfalarının hazırlık kuralları (veritabanından bağımsız kısım).</summary>
public class GrupVeMeclisSayfaTestleri
{
    private static GrupSonuclari Sonuclar(GrupSorgusu sorgu)
    {
        var e = RaporBelgeleriTestleri.OrnekErime();
        var g = GrupHesabi.Hesapla(e.Rapor!, e.IlceErimesi[OyTuru.BelediyeMeclisi].ToDictionary(x => x.Yil, x => x.Oran), GrupEsikleri.Varsayilan);
        return new GrupSonuclari(e, g, OncelikHesabi.Hesapla(g, e.Sinir), sorgu);
    }

    [Fact]
    public void GrupSorgusu_AdrestenOkunur_GecersizGrupYokSayilir()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues> { ["ilce"] = "34-ESENYURT", ["grup"] = "erozyon", ["donem"] = "2019" });

        var s = GrupSorgusu.Oku(q);

        Assert.Equal((MahalleGrubu.Erozyon, (int?)2019), (s.GrupSecimi!.Value, s.Donem));
        Assert.Equal("ilce=34-ESENYURT&grup=erozyon&donem=2019", s.SorguMetni());
        Assert.Null((s with { Grup = "kalee" }).GrupSecimi);
    }

    [Fact]
    public void Kareler_DortKare_BugunkuGucVeEgilimle()
    {
        // Örnek: A (2009) taban 1 → son 0,5, ilçe son 0,3 → bugün güçlü, geriledi. B (2019) taban 1 → 0,5: aynı kare.
        var k = Sonuclar(new GrupSorgusu("34-ESENYURT", "belediye-meclisi")).Kareler();

        Assert.Equal(4, k.Count);
        var gg = k.Single(x => x.Guclu && !x.Yukseldi);
        Assert.Equal(("Güçlü ama gerileyen", 2, 400L), (gg.Ad, gg.Sayi, gg.ToplamOy));
        Assert.All(k.Where(x => x != gg), x => Assert.Equal(0, x.Sayi));
    }

    [Theory]
    [InlineData(null, null, 2)]
    [InlineData(null, 2009, 1)]
    [InlineData("kale", null, 1)]      // A: 1 oy kayıp, eşiğin altında → değişmedi → kale
    [InlineData("erozyon", null, 0)]   // B'nin grubu yok: ilçenin 2019 oranı örnekte yok
    public void SuzulmusOncelik_GrupVeDonem(string? grup, int? donem, int beklenenSatir)
    {
        var s = Sonuclar(new GrupSorgusu("34-ESENYURT", "belediye-meclisi", grup, donem));

        var l = s.SuzulmusOncelik();

        Assert.Equal(beklenenSatir, l.Sum(x => x.Satirlar.Count));
        Assert.All(l, x => Assert.NotEmpty(x.Satirlar));   // boş dönem listesi gösterilmez
    }

    [Fact]
    public void GucEgilimGrafigi_DonemBasinaSeri_EksenSifiriIcerir()
    {
        var g = RaporGrafikleri.GucEgilim(Sonuclar(new GrupSorgusu("34-ESENYURT", "belediye-meclisi")))!;

        Assert.Equal(["dönem 2009–2024", "dönem 2019–2024"], g.Seriler.Select(x => x.Ad));
        Assert.Equal([0, 1], g.Noktalar.Select(n => n.Seri));
        var svg = SvgCizici.Ciz(g, Gorunum.Varsayilan);
        Assert.Contains(">Güçlü ama gerileyen</text>", svg);
        Assert.Contains("<title>A: Bugünkü güç +0,20, Eğilim −0,50 · Kale · dönem 2009–2024</title>", svg);
    }

    [Theory]
    [InlineData(new[] { 0.1, 0.4 }, 0, 0.5)]        // hepsi artı: alt sıfır
    [InlineData(new[] { -3.54, 0.27 }, -4, 1)]       // uç değer: sıfır ve iki yan görünür
    [InlineData(new[] { -0.2, -0.05 }, -0.25, 0)]    // hepsi eksi: üst sınır sıfır
    public void Aralik_SifirIcerir(double[] degerler, double alt, double ust)
    {
        var (a, u, _) = SvgCizici.Aralik(degerler.Select(d => (decimal)d));

        Assert.Equal(((decimal)alt, (decimal)ust), (a, u));
    }

    [Theory]
    [InlineData(-0.004, "0,00")]
    [InlineData(0.004, "0,00")]
    [InlineData(-0.005, "−0,01")]
    [InlineData(0.16, "+0,16")]
    public void Puan_IsaretYuvarlanmisDegere(double v, string beklenen) => Assert.Equal(beklenen, Metinler.Puan((decimal)v));

    [Fact]
    public void Dagilim_UstUsteBinecekAdYazilmaz()
    {
        var g = new DagilimGrafigi("T", null, "Eğilim: x", "Bugünkü güç: y", [("d", 0)],
            [new("BİRİNCİ MAH.", -0.44m, -0.15m, 0, Etiketli: true), new("İKİNCİ MAH.", -0.44m, -0.152m, 0, Etiketli: true),
             new("UZAK MAH.", 0.2m, 0.2m, 0, Etiketli: true)], ["a", "b", "c", "d"]);

        var svg = SvgCizici.Ciz(g, Gorunum.Varsayilan);

        Assert.Contains(">BİRİNCİ MAH.</text>", svg);
        Assert.DoesNotContain(">İKİNCİ MAH.</text>", svg);     // aynı yere düşüyor: yalnızca ipucunda
        Assert.Contains("<title>İKİNCİ MAH.:", svg);
        Assert.Contains(">UZAK MAH.</text>", svg);
    }

    // ---------------- Meclis ----------------

    private static readonly IlceSecenegi Ilce = RaporBelgeleriTestleri.Ilce;
    private static readonly SecimSecenegi S2024 = RaporBelgeleriTestleri.Secim2024;
    private static readonly SecimSecenegi S2019 = new(3, 2019, new DateOnly(2019, 3, 31), "2019 Yerel Seçimleri", SecimTuru.Yerel);
    private static readonly MeclisListesi[] Listeler = [new("CHP", 221_877), new("AK PARTİ", 176_194), new("ZAFER PARTİSİ", 18_220), new("DSP", 975)];
    private static readonly MeclisUyeTanimlari Tanimlar = new([new MeclisUyeSayisi("34-ESENYURT", 2024, 45, false, "kullanıcı varsayımı")]);

    private static MeclisSonuclari Hesapla(SecimSecenegi secim, MeclisSorgusu sorgu, IReadOnlyList<MeclisListesi>? listeler = null) =>
        MeclisHazirlayici.Hesapla([Ilce], Ilce, [S2024, S2019], secim, listeler ?? Listeler, sorgu, Tanimlar);

    [Fact]
    public void Uye_TanimDosyasindan_DogrulanmadiysaUyari()
    {
        var m = Hesapla(S2024, new MeclisSorgusu(null, null, null, null, null));

        Assert.Equal((45, UyeSayisiKaynagi.Dogrulanmadi), (m.UyeSayisi, m.UyeKaynagi));
        Assert.Equal("2024 için 45 üye varsayımdır, YSK sonucundan doğrulanmadı (kullanıcı varsayımı). Dağılım bu sayıya bağlıdır.", m.UyeUyarisi);
        Assert.Equal((28, 17), (m.Mevcut!.Koltuk("CHP"), m.Mevcut.Koltuk("AK PARTİ")));
    }

    [Fact]
    public void Uye_TanimdaYoksaIlceninSonSayisi_TahminUyarisi()
    {
        var m = Hesapla(S2019, new MeclisSorgusu(null, 3, null, null, null));

        Assert.Equal((45, UyeSayisiKaynagi.Tahmin), (m.UyeSayisi, m.UyeKaynagi));
        Assert.StartsWith("2019 için meclis üye sayısı tanım dosyasında yok; 45 üyeyle hesaplandı (doğrulanmadı).", m.UyeUyarisi);
    }

    [Fact]
    public void Uye_SayfadaSecilir_KanundaOlmayanYokSayilir()
    {
        var secilen = Hesapla(S2019, new MeclisSorgusu(null, 3, 37, null, null));
        var gecersiz = Hesapla(S2019, new MeclisSorgusu(null, 3, 40, null, null));

        Assert.Equal((37, UyeSayisiKaynagi.Secildi), (secilen.UyeSayisi, secilen.UyeKaynagi));
        Assert.Equal(37, secilen.Sorgu.Uye);
        Assert.Contains("tanım dosyasında bu yıl için sayı yok", secilen.UyeUyarisi);
        Assert.Equal(UyeSayisiKaynagi.Tahmin, gecersiz.UyeKaynagi);
    }

    [Fact]
    public void DogrulanmisUye_UyariYok()
    {
        var t = new MeclisUyeTanimlari([new MeclisUyeSayisi("34-ESENYURT", 2024, 45, true, "sonuc.ysk.gov.tr")]);

        var m = MeclisHazirlayici.Hesapla([Ilce], Ilce, [S2024], S2024, Listeler, new MeclisSorgusu(null, null, null, null, null), t);

        Assert.Null(m.UyeUyarisi);
    }

    [Fact]
    public void EkOy_SenaryoVeEsikler_KaynagaGore()
    {
        var yeni = Hesapla(S2024, new MeclisSorgusu(null, null, null, 60_000, null));
        var buyuk = Hesapla(S2024, new MeclisSorgusu(null, null, null, 60_000, MeclisSorgusu.EnBuyukParti));

        Assert.Equal(1, yeni.Senaryo!.Koltuk("DSP"));
        Assert.Equal(2, buyuk.Senaryo!.Koltuk("DSP"));                           // toplam artmadığı için daha çok
        Assert.Equal(yeni.Senaryo.GecerliOy, yeni.Mevcut!.GecerliOy + 60_000);
        Assert.Equal(buyuk.Senaryo.GecerliOy, buyuk.Mevcut!.GecerliOy);
        Assert.Equal([1, 2, 3], yeni.Esikler.Select(e => e.Koltuk));
        Assert.True(yeni.BarajIcin > buyuk.BarajIcin);
        Assert.Equal("ek-oy=60000&kaynak=en-buyuk-parti", Regex.Replace(buyuk.Sorgu.SorguMetni(), @"^ilce=[^&]+&secim=\d+&", ""));
    }

    [Fact]
    public void EkOy_EnBuyukPartininOyundanFazla_Hata()
    {
        var m = Hesapla(S2024, new MeclisSorgusu(null, null, null, 300_000, MeclisSorgusu.EnBuyukParti));

        Assert.Null(m.Senaryo);
        Assert.Equal("Ek oy (300.000) en büyük partinin (CHP) oyundan (221.877) fazla.", m.Hata);
        Assert.NotNull(m.Mevcut);
    }

    [Fact]
    public void DspListesiYoksa_DagilimVarEkOyYok()
    {
        var m = Hesapla(S2024, new MeclisSorgusu(null, null, null, 10_000, null), [.. Listeler.Where(l => l.Ad != "DSP")]);

        Assert.False(m.DspVar);
        Assert.NotNull(m.Mevcut);
        Assert.Null(m.Senaryo);
        Assert.Equal("2024 belediye meclisi seçiminde DSP'nin listesi yok; ek oy hesaplanamaz.", m.Hata);
    }

    [Fact]
    public void UygulamaKlasorundekiTanimDosyasi_Okunur()
    {
        // Web projesi Importer/Tanimlar/meclis-uye-sayilari.csv dosyasını çıktı klasörüne kopyalar; test projesi de alır.
        var t = MeclisUyeTanimlari.Yukle();

        Assert.Contains(t.Kayitlar, k => k.IlceKod == "34-ESENYURT" && k.Yil == 2024 && k.UyeSayisi == 45);
    }
}
