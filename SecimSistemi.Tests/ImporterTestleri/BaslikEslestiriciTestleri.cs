using SecimSistemi.Domain;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Importer.Kontrol;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Tests.ImporterTestleri;

public class BaslikEslestiriciTestleri
{
    private static readonly DateOnly Secim2014 = new(2014, 3, 30);
    private static readonly DateOnly Secim2019 = new(2019, 3, 31);
    private static readonly DateOnly Secim2024 = new(2024, 3, 31);

    /// <summary>Depodaki gerçek tanım dosyası: testler kararların kendisini de doğrular.</summary>
    private static readonly BaslikEslestirici Eslestirici = new(BaslikTanimlari.Yukle());

    private static List<Baslik> Basliklar(params string[] adlar) =>
        adlar.Select((ad, i) => new Baslik(i + 4, ad)).ToList();

    private static Kalem TekKalem(string baslik, DateOnly tarih, OyTuru oyTuru = OyTuru.BelediyeMeclisi)
    {
        var sonuc = Eslestirici.Eslestir(Basliklar(baslik), tarih, oyTuru);
        Assert.True(sonuc.BasariliMi, $"'{baslik}' eşlenemedi");
        return Assert.Single(sonuc.Eslesmeler).Kalem;
    }

    [Theory]
    [InlineData("DEM Parti", "DEM PARTİ")]
    [InlineData("  ak   parti ", "AK PARTİ")]
    [InlineData("DSP", "DSP")]
    public void YazimFarki_AyniPartiyeEslenir(string baslik, string parti)
    {
        Assert.Equal(Kalem.Parti(parti), TekKalem(baslik, Secim2024));
    }

    [Fact]
    public void Ip2014_VatanPartisidir()
    {
        Assert.Equal(Kalem.Parti("VATAN PARTİSİ"), TekKalem("İP", Secim2014));
        Assert.Equal(Kalem.Parti("VATAN PARTİSİ"), TekKalem("VATAN PARTİSİ", Secim2019));
    }

    [Fact]
    public void Ip2014Sonrasinda_TaninmazVeDurdurur()
    {
        // 2019'da "İP" yazılırsa İşçi Partisi mi İYİ Parti mi belirsiz: tahmin edilmez, durulur.
        var sonuc = Eslestirici.Eslestir(Basliklar("İP"), Secim2019, OyTuru.BelediyeMeclisi);

        Assert.False(sonuc.BasariliMi);
        Assert.Equal("İP", Assert.Single(sonuc.Bilinmeyenler).Ham);
    }

    [Fact]
    public void IyiParti2017Oncesinde_Taninmaz()
    {
        Assert.False(Eslestirici.Eslestir(Basliklar("İYİ PARTİ"), Secim2014, OyTuru.BelediyeMeclisi).BasariliMi);
        Assert.Equal(Kalem.Parti("İYİ PARTİ"), TekKalem("İYİ PARTİ", Secim2024));
    }

    [Fact]
    public void Bagimsiz_YalnizcaKendiSecimiVeOyTurundeTaninir()
    {
        Assert.Equal(Kalem.Aday("MEHMET ŞÜKRÜ ENGİN"), TekKalem("MEHMET ŞÜKRÜ ENGİN", new DateOnly(2009, 3, 29), OyTuru.BelediyeBaskani));
        Assert.Equal(Kalem.Aday("MEHMET ŞÜKRÜ ENGİN"), TekKalem("MEHMET ŞÜKRÜ ENGİN", Secim2014, OyTuru.BelediyeMeclisi));

        // 2014'te meclis adayıydı, başkan adayı değil.
        Assert.False(Eslestirici.Eslestir(Basliklar("MEHMET ŞÜKRÜ ENGİN"), Secim2014, OyTuru.BelediyeBaskani).BasariliMi);
    }

    [Fact]
    public void SutunNumaralariKorunur()
    {
        var sonuc = Eslestirici.Eslestir(Basliklar("CHP", "AK PARTİ", "DSP"), Secim2024, OyTuru.BelediyeMeclisi);

        Assert.Equal([4, 5, 6], sonuc.Eslesmeler.Select(e => e.SutunNo));
        Assert.Equal("DSP", Assert.IsType<PartiSutunu>(sonuc.Eslesmeler[2]).Parti.KisaAd);
    }

    [Fact]
    public void BilinmeyenBasliklar_HepsiListelenirVeDurdurur()
    {
        var sonuc = Eslestirici.Eslestir(
            Basliklar("AK PARTİ", "YENİ PARTİ", "DSP", "AYŞE YILMAZ", ""), Secim2024, OyTuru.BelediyeMeclisi);

        Assert.Equal([5, 7, 8], sonuc.Bilinmeyenler.Select(b => b.SutunNo));
        Assert.Equal(2, sonuc.Eslesmeler.Count);

        var hata = Assert.Throws<BilinmeyenBaslikHatasi>(() =>
            sonuc.BasarisizsaDur("2024_yerel_belediye-meclisi_esenyurt.xlsx", Secim2024, OyTuru.BelediyeMeclisi));
        Assert.Contains("5. sütun: 'YENİ PARTİ'", hata.Message);
        Assert.Contains("7. sütun: 'AYŞE YILMAZ'", hata.Message);
        Assert.Contains("8. sütun: ''", hata.Message);
        // Tanım dosyasına yapıştırılmaya hazır taslak satır.
        Assert.Contains("YENİ PARTİ;;;;;2024;belediye-meclisi", hata.Message);
    }

    [Fact]
    public void TumBasliklarTaniniyorsa_Durmaz()
    {
        var sonuc = Eslestirici.Eslestir(Basliklar("AK PARTİ", "CHP", "DSP"), Secim2024, OyTuru.BelediyeMeclisi);

        sonuc.BasarisizsaDur("x.xlsx", Secim2024, OyTuru.BelediyeMeclisi);
        Assert.True(sonuc.BasariliMi);
    }

    [Fact]
    public void IkiSutunAyniPartiye_HataVerir()
    {
        var sonuc = Eslestirici.Eslestir(Basliklar("İP", "VATAN PARTİSİ"), Secim2014, OyTuru.BelediyeMeclisi);

        Assert.False(sonuc.BasariliMi);
        Assert.Empty(sonuc.Bilinmeyenler);
        Assert.Contains("VATAN PARTİSİ", Assert.Single(sonuc.Hatalar));
    }
}
