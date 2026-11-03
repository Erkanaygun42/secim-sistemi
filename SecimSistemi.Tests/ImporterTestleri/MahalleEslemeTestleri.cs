using SecimSistemi.Domain;
using SecimSistemi.Importer;
using SecimSistemi.Importer.Mahalleler;

namespace SecimSistemi.Tests.ImporterTestleri;

public class MahalleEslemeTestleri
{
    private static IReadOnlyList<MahalleEslemeSatiri> Oku(params string[] satirlar) =>
        MahalleEslemeDosyasi.Oku(new StringReader(string.Join("\n", [MahalleEslemeDosyasi.BaslikSatiri, .. satirlar])), "test.csv");

    // ---- Ad benzerliği ----

    [Theory]
    [InlineData("ARDIÇLI MAH.", "ARDIÇLI")]
    [InlineData("  Yeşilkent   Mahallesi ", "YEŞİLKENT")]
    [InlineData("KIRAÇ KÖYÜ", "KIRAÇ")]
    public void Normallestir_EkVeBuyukHarf(string ad, string beklenen)
    {
        Assert.Equal(beklenen, AdBenzerligi.Normallestir(ad));
    }

    [Fact]
    public void Benzerlik_AyniAdBirDahaAzBenzerDusuk()
    {
        Assert.Equal(1, AdBenzerligi.Benzerlik("ARDIÇLI MAH.", "Ardıçlı Mahallesi"));
        var yakin = AdBenzerligi.Benzerlik("YEŞİLKENT MAH.", "YEŞİLKENT YENİ MAH.");
        var uzak = AdBenzerligi.Benzerlik("YEŞİLKENT MAH.", "ÇINAR MAH.");
        Assert.True(yakin > 0.85, $"yakın {yakin}");
        Assert.True(uzak < 0.6, $"uzak {uzak}");
    }

    [Fact]
    public void JaroWinkler_BilinenDeger()
    {
        // Jaro-Winkler'in standart örneği: MARTHA / MARHTA = 0,961.
        Assert.Equal(0.961, AdBenzerligi.JaroWinkler("MARTHA", "MARHTA"), 3);
    }

    // ---- csv ----

    [Fact]
    public void Satirlar_Okunur()
    {
        var s = Assert.Single(Oku("119319;ARDIÇLI MAH.;147450;YENİ MAH.;bolunme;2017-06-01;onayli;0.82;Resmî Gazete"));

        Assert.Equal((119319, 147450, EslemeTuru.Bolunme, EslemeDurumu.Onayli), (s.EskiKod, s.YeniKod, s.Tur, s.Durum));
        Assert.Equal(new DateOnly(2017, 6, 1), s.Tarih);
        Assert.Equal(0.82, s.Benzerlik);
    }

    [Fact]
    public void OneriSatirindaTurVeTarihBosOlabilir()
    {
        var s = Assert.Single(Oku("1;A MAH.;2;B MAH.;;;oneri;;"));
        Assert.Null(s.Tur);
        Assert.Null(s.Tarih);
    }

    [Theory]
    [InlineData("1;A;2;B;;;onayli;;")]           // onaylıda tur yok
    [InlineData("1;A;1;A;bolunme;;onayli;;")]    // kendine eşleme
    [InlineData("x;A;2;B;bolunme;;onayli;;")]    // kod sayı değil
    [InlineData("1;A;2;B;bolunme;;tamam;;")]     // durum yanlış
    [InlineData("1;A;2;B;ayrilma;;onayli;;")]    // tur yanlış
    [InlineData("1;A;2;B;bolunme;01.06.2017;onayli;;")] // tarih biçimi
    [InlineData("1;A;2;B;bolunme;;onayli")]      // alan eksik
    public void HataliSatir_HataVerir(string satir)
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku(satir));
        Assert.Contains("2. satır", hata.Message);
    }

    [Fact]
    public void AyniCiftIkiYondenYazilirsa_HataVerir()
    {
        var hata = Assert.Throws<YuklemeHatasi>(() => Oku("1;A;2;B;bolunme;;onayli;;", "2;B;1;A;;;oneri;;"));
        Assert.Contains("birden çok kez", hata.Message);
    }

    [Fact]
    public void YazipOkuyunca_AyniSatirlar()
    {
        var satirlar = Oku(
            "119319;ARDIÇLI MAH.;147450;YENİ MAH.;bolunme;2017-06-01;onayli;0.82;not",
            "1;A MAH.;2;B MAH.;;;oneri;0.91;");
        var yazici = new StringWriter();

        MahalleEslemeDosyasi.Yaz(yazici, satirlar);

        Assert.Equal(satirlar, MahalleEslemeDosyasi.Oku(new StringReader(yazici.ToString()), "test.csv"));
    }

    [Fact]
    public void OnerileriEkle_OnayliVeRetSatirlarinaDokunmaz()
    {
        var mevcut = Oku("1;A;2;B;bolunme;;onayli;;benim notum", "1;A;3;C;;;ret;;", "1;A;4;D;;;oneri;0.85;");
        MahalleEslemeSatiri[] oneriler =
        [
            new(2, "B", 1, "A", null, null, EslemeDurumu.Oneri, 0.9, "ters yön, aynı çift"),
            new(1, "A", 3, "C", null, null, EslemeDurumu.Oneri, 0.9, "reddedilmişti"),
            new(1, "A", 4, "D", null, null, EslemeDurumu.Oneri, 0.9, "zaten önerilmiş"),
            new(1, "A", 5, "E", null, null, EslemeDurumu.Onayli, 0.9, "yeni"),
        ];

        var (satirlar, eklenen) = MahalleEslemeDosyasi.OnerileriEkle(mevcut, oneriler);

        Assert.Equal(1, eklenen);
        Assert.Equal(mevcut, satirlar.Take(3));
        var yeni = satirlar[3];
        Assert.Equal((5, EslemeDurumu.Oneri), (yeni.YeniKod, yeni.Durum)); // öneri hiçbir zaman onaylı eklenmez
    }

    // ---- öneri ve ipucu ----

    private static MahalleYillari M(int kod, string ad, params (int Yil, long Oy)[] yillar) =>
        new(kod, ad, yillar.ToDictionary(y => y.Yil, y => new MahalleYilVerisi(1, y.Oy)));

    [Fact]
    public void SonradanCikanMahalle_BenzerAdliEskiyeOnerilir()
    {
        var veri = new IlceMahalleVerisi([2014, 2019],
        [
            M(1, "YEŞİLKENT MAH.", (2014, 1000), (2019, 400)),
            M(2, "ÇINAR MAH.", (2014, 1000), (2019, 1000)),
            M(3, "YEŞİLKENT YENİ MAH.", (2019, 600)),
        ]);

        var o = Assert.Single(EslemeOnerici.Oner(veri));

        Assert.Equal((1, 3, EslemeDurumu.Oneri), (o.EskiKod, o.YeniKod, o.Durum));
        Assert.Null(o.Tur);
        Assert.Contains("2014: 50,0%", o.Not); // eski mahallenin ilçe payı
    }

    [Fact]
    public void BenzerAdYoksa_OneriYok()
    {
        var veri = new IlceMahalleVerisi([2014, 2019],
        [
            M(1, "ARDIÇLI MAH.", (2014, 1000), (2019, 400)),
            M(3, "ZAFER MAH.", (2019, 600)),
        ]);

        Assert.Empty(EslemeOnerici.Oner(veri));
    }

    [Fact]
    public void HerSecimdeVarOlan_OneriUretmez()
    {
        var veri = new IlceMahalleVerisi([2014, 2019],
        [
            M(1, "YEŞİLKENT MAH.", (2014, 1000), (2019, 1000)),
            M(2, "YEŞİLKENT 2 MAH.", (2014, 1000), (2019, 1000)),
        ]);

        Assert.Empty(EslemeOnerici.Oner(veri));
    }

    [Fact]
    public void Ipucu_PayiDusenEskiMahalleyiListeler()
    {
        var veri = new IlceMahalleVerisi([2014, 2019],
        [
            M(1, "ARDIÇLI MAH.", (2014, 1000), (2019, 300)),   // pay 50% → 15%
            M(2, "ÇINAR MAH.", (2014, 1000), (2019, 1000)),    // 50% → 50%
            M(3, "ZAFER MAH.", (2019, 700)),
        ]);

        var ipuclari = EslemeOnerici.Ipuclari(veri);

        Assert.Contains(ipuclari, s => s.Contains("1 yeni mahalle"));
        Assert.Contains(ipuclari, s => s.Contains("ARDIÇLI"));
        Assert.DoesNotContain(ipuclari, s => s.Contains("ÇINAR"));
    }
}
