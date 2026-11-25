using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

/// <summary>
/// Belediye meclisi dağılımı, 2972 sayılı Kanun m.5/b, m.10/c, m.23/a. Örnekler elle hesaplandı (açıklamalarda).
/// </summary>
public class MeclisHesabiTestleri
{
    private static MeclisListesi L(string ad, long oy, bool bagimsiz = false) => new(ad, oy, bagimsiz);

    [Fact]
    public void KalanOylaBolme_DuzDHondttanFarkli()
    {
        // Geçerli 1.000, baraj 100. Kalan: A 350, B 200, C 50, D 0 (tam %10: geriye oy kalmaz, dışarıda).
        // 9 üye: 1 kontenjan, 8 bölmeyle. Paylar: 350A 200B 175A 116,7A 100B 87,5A 70A 66,7B | 58,3A ...
        // → A 5 + kontenjan 1 = 6, B 3, C 0. (Düz D'Hondt ham oyla C'ye 1 üye verirdi: 450 300 225 150 150 150 ...)
        var s = MeclisHesabi.Dagit([L("A", 450), L("B", 300), L("C", 150), L("D", 100)], 9);

        Assert.Equal((1, 100m), (s.KontenjanSayisi, s.BarajOyu));
        Assert.Equal([("A", 5, 1), ("B", 3, 0), ("C", 0, 0), ("D", 0, 0)], s.Listeler.Select(l => (l.Ad, l.Koltuk, l.Kontenjan)));
        Assert.Equal((true, 50m), (s.Liste("C")!.BarajiGecti, s.Liste("C")!.KalanOy));
        Assert.False(s.Liste("D")!.BarajiGecti);
        Assert.Equal("A", s.KontenjanAlan);
        Assert.Empty(s.KuraListeleri);
        Assert.Equal(350m / 5 - 350m / 5 + 200m / 3, s.SonPay); // son verilen pay: B'nin 3. payı 66,67
    }

    [Fact]
    public void ToplamUyeSayisiTutar()
    {
        var s = MeclisHesabi.Dagit([L("A", 228_075), L("B", 181_802), L("C", 20_000), L("D", 34_184)], 45);

        Assert.Equal(45, s.Listeler.Sum(l => l.Toplam));
        Assert.Equal(5, s.Liste("A")!.Kontenjan);
    }

    [Fact]
    public void SonKoltuktaEsitPay_KuraBildirilir()
    {
        // Geçerli 100, baraj 10. Kalan: A 30, B 30, C 10. 9 üye → 8 bölmeyle.
        // 30A 30B 15A 15B 10A 10B 10C (7 koltuk) | 8. koltuk: 7,5A = 7,5B → kura.
        var s = MeclisHesabi.Dagit([L("A", 40), L("B", 40), L("C", 20)], 9);

        Assert.Equal(["A", "B"], s.KuraListeleri.Order());
        Assert.Equal(8, s.Listeler.Sum(l => l.Koltuk));
        Assert.Equal(1, s.Liste("C")!.Koltuk);
    }

    [Fact]
    public void Bagimsiz_EnCokBirUye_KontenjanAlmaz()
    {
        // Geçerli 1.000, kalan: A 500, X (bağımsız) 300. X tek kişi: 1 üye; kalan 7 bölmeli üye ve kontenjan A'ya.
        var s = MeclisHesabi.Dagit([L("A", 600), L("X", 400, bagimsiz: true)], 9);

        Assert.Equal((7, 1), (s.Liste("A")!.Koltuk, s.Liste("A")!.Kontenjan));
        Assert.Equal((1, 0), (s.Liste("X")!.Koltuk, s.Liste("X")!.Kontenjan));
    }

    [Fact]
    public void Kontenjan_EnCokOyAlanParti_BagimsizDegil()
    {
        var s = MeclisHesabi.Dagit([L("X", 500, bagimsiz: true), L("A", 300), L("B", 200)], 9);

        Assert.Equal("A", s.KontenjanAlan);
        Assert.Equal(1, s.Liste("A")!.Kontenjan);
    }

    [Theory]
    [InlineData(10_000, 9)]
    [InlineData(10_001, 11)]
    [InlineData(20_000, 11)]
    [InlineData(50_000, 15)]
    [InlineData(100_000, 25)]
    [InlineData(250_000, 31)]
    [InlineData(500_000, 37)]
    [InlineData(1_000_000, 45)]
    [InlineData(1_000_001, 55)]
    public void UyeSayisi_Madde5b(long nufus, int uye) => Assert.Equal(uye, MeclisHesabi.UyeSayisi(nufus));

    [Theory]
    [InlineData(9, 1)]
    [InlineData(11, 1)]
    [InlineData(15, 2)]
    [InlineData(25, 3)]
    [InlineData(31, 3)]
    [InlineData(37, 4)]
    [InlineData(45, 5)]
    [InlineData(55, 6)]
    public void Kontenjan_Madde10c(int uye, int kontenjan) => Assert.Equal(kontenjan, MeclisHesabi.KontenjanSayisi(uye));

    [Fact]
    public void KanundaOlmayanUyeSayisi_Hata() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MeclisHesabi.Dagit([L("A", 1)], 40));

    private static readonly MeclisListesi[] Ornek = [L("A", 600), L("B", 350), L("DSP", 50)];

    [Fact]
    public void BarajIcinGerekenOy_YeniSecmen()
    {
        // 10 × (50 + X) > 1.000 + X → 9X > 500 → X ≥ 56. X = 55'te 10 × 105 = 1.050 < 1.055: geçemez.
        Assert.Equal(56, MeclisHesabi.BarajIcinGerekenOy(Ornek, 9, "DSP", EkOyKaynagi.YeniSecmen));
        Assert.False(MeclisHesabi.Dagit(MeclisHesabi.EkOyEkle(Ornek, "DSP", 55, EkOyKaynagi.YeniSecmen), 9).Liste("DSP")!.BarajiGecti);
    }

    [Fact]
    public void BarajIcinGerekenOy_EnBuyukPartiden()
    {
        // Toplam 1.000 sabit: 10 × (50 + X) > 1.000 → X ≥ 51; oylar A'dan düşer.
        Assert.Equal(51, MeclisHesabi.BarajIcinGerekenOy(Ornek, 9, "DSP", EkOyKaynagi.EnBuyukParti));
        var l = MeclisHesabi.EkOyEkle(Ornek, "DSP", 51, EkOyKaynagi.EnBuyukParti);
        Assert.Equal([549L, 350L, 101L], l.Select(x => x.Oy));
        Assert.Equal(1000, l.Sum(x => x.Oy));
    }

    [Fact]
    public void BarajiZatenGeciyorsa_Sifir() =>
        Assert.Equal(0, MeclisHesabi.BarajIcinGerekenOy(Ornek, 9, "A", EkOyKaynagi.YeniSecmen));

    [Fact]
    public void KoltukEsikleri_ArtanVeTutarli()
    {
        var e = MeclisHesabi.KoltukEsikleri(Ornek, 9, "DSP", EkOyKaynagi.YeniSecmen, 4);

        Assert.Equal([1, 2, 3, 4], e.Select(x => x.Koltuk));
        Assert.True(e.Zip(e.Skip(1)).All(p => p.First.EkOy <= p.Second.EkOy));
        foreach (var x in e)
        {
            // Bir eşikte birden çok üye gelebilir: en büyük parti olunca kontenjan da gelir (burada 4. eşikte 3 → 5).
            Assert.True(MeclisHesabi.Dagit(MeclisHesabi.EkOyEkle(Ornek, "DSP", x.EkOy, EkOyKaynagi.YeniSecmen), 9).Koltuk("DSP") >= x.Koltuk);
            Assert.True(MeclisHesabi.Dagit(MeclisHesabi.EkOyEkle(Ornek, "DSP", x.EkOy - 1, EkOyKaynagi.YeniSecmen), 9).Koltuk("DSP") < x.Koltuk);
        }
    }

    [Fact]
    public void EnBuyukPartidenAlirken_OyuYetmezseUlasilamaz()
    {
        // A'nın 600 oyu bitse de DSP 9 üyenin hepsini alamaz (B kalır).
        Assert.Null(MeclisHesabi.KoltukIcinGerekenOy(Ornek, 9, "DSP", 9, EkOyKaynagi.EnBuyukParti));
        Assert.Throws<ArgumentException>(() => MeclisHesabi.EkOyEkle(Ornek, "DSP", 601, EkOyKaynagi.EnBuyukParti));
    }
}
