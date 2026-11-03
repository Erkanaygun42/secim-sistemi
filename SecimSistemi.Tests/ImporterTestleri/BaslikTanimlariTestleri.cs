using SecimSistemi.Domain;
using SecimSistemi.Importer;
using SecimSistemi.Importer.Eslestirme;

namespace SecimSistemi.Tests.ImporterTestleri;

public class BaslikTanimlariTestleri
{
    internal static BaslikTanimlari Oku(params string[] satirlar) =>
        BaslikTanimlari.Oku(new StringReader(string.Join("\n", [BaslikTanimlari.BaslikSatiri, .. satirlar])), "test.csv");

    [Fact]
    public void AyniPartiAdi_TekPartiNesnesiPaylasir()
    {
        var t = Oku(
            "İP;parti;VATAN PARTİSİ;;2014-12-31;;",
            "Vatan Partisi;parti;VATAN PARTİSİ;;;;");

        Assert.Equal(2, t.PartiEslemeleri.Count);
        Assert.Same(t.PartiEslemeleri[0].Parti, t.PartiEslemeleri[1].Parti);
        Assert.Equal("VATAN PARTİSİ", t.PartiEslemeleri[1].HamAd);
        Assert.Single(t.Partiler);
    }

    [Fact]
    public void YorumVeBosSatirlar_Atlanir()
    {
        var t = BaslikTanimlari.Oku(new StringReader($"# açıklama\n\n{BaslikTanimlari.BaslikSatiri}\n# partiler\nDSP;parti;DSP;;;;\n"), "test.csv");

        Assert.Single(t.PartiEslemeleri);
    }

    [Fact]
    public void Bagimsiz_YilVeOyTuruyleOkunur()
    {
        var t = Oku("Fatma Ragibe Kanıkuru Loğoğlu;bagimsiz;;;;2024;buyuksehir-baskani");

        var b = Assert.Single(t.Bagimsizlar);
        Assert.Equal("FATMA RAGİBE KANIKURU LOĞOĞLU", b.AdSoyad);
        Assert.Equal(2024, b.Yil);
        Assert.Equal(OyTuru.BuyuksehirBaskani, b.OyTuru);
    }

    [Theory]
    [InlineData("DSP;partii;DSP;;;;")]                             // tur yanlış
    [InlineData("DSP;parti;;;;;")]                                 // parti adı yok
    [InlineData("DSP;parti;DSP;;;2024;")]                          // partide yıl
    [InlineData("DSP;parti;DSP;2024-31-03;;;")]                    // tarih biçimi
    [InlineData("DSP;parti;DSP;2024-01-01;2020-01-01;;")]          // başlangıç bitişten sonra
    [InlineData("AHMET YILMAZ;bagimsiz;;;;;belediye-baskani")]     // bağımsızda yıl yok
    [InlineData("AHMET YILMAZ;bagimsiz;;;;2024;muhtar")]           // oy türü tanımsız
    [InlineData("AHMET YILMAZ;bagimsiz;CHP;;;2024;belediye-baskani")] // bağımsızda parti
    [InlineData("DSP;parti;DSP;;")]                                // alan eksik
    public void HataliSatir_HataVerir(string satir)
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku(satir));
        Assert.Contains("2. satır", hata.Message);
    }

    [Fact]
    public void CakisanTarihAraligi_HataVerir()
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku(
            "İP;parti;VATAN PARTİSİ;;2017-12-31;;",
            "İP;parti;İYİ PARTİ;2017-10-25;;;"));
        Assert.Contains("çakışıyor", hata.Message);
    }

    [Fact]
    public void CakismayanTarihAraligi_Kabul()
    {
        var t = Oku(
            "İP;parti;VATAN PARTİSİ;;2014-12-31;;",
            "İP;parti;İYİ PARTİ;2017-10-25;;;");
        Assert.Equal(2, t.Partiler.Count);
    }

    [Fact]
    public void TekrarlananBagimsiz_HataVerir()
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku(
            "AHMET YILMAZ;bagimsiz;;;;2024;belediye-baskani",
            "Ahmet  Yılmaz;bagimsiz;;;;2024;belediye-baskani"));
        Assert.Contains("tekrarı", hata.Message);
    }

    [Fact]
    public void HemPartiHemBagimsiz_HataVerir()
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku(
            "MİLLET;parti;MİLLET;;;;",
            "MİLLET;bagimsiz;;;;2024;belediye-baskani"));
        Assert.Contains("hem parti hem bağımsız", hata.Message);
    }

    [Fact]
    public void BirdenCokHata_HepsiBirlikteBildirilir()
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku("A;x;;;;;", "B;y;;;;;"));
        Assert.Contains("2. satır", hata.Message);
        Assert.Contains("3. satır", hata.Message);
    }

    [Fact]
    public void BaslikSatiriYanlis_HataVerir()
    {
        Assert.Throws<YuklemeHatasi>(() => BaslikTanimlari.Oku(new StringReader("ad;tur\nDSP;parti"), "test.csv"));
    }

    [Fact]
    public void DepodakiTanimDosyasi_HatasizYuklenir()
    {
        var t = BaslikTanimlari.Yukle();

        Assert.Equal(40, t.PartiEslemeleri.Count);
        Assert.Equal(39, t.Partiler.Count);
        Assert.Equal(88, t.Bagimsizlar.Count);
        Assert.Contains(t.Partiler, p => p.KisaAd == "DSP");
    }
}
