using SecimSistemi.Domain;
using SecimSistemi.Importer;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Tests.ImporterTestleri;

public class DosyaKimligiTestleri
{
    [Fact]
    public void SandikDosyasi_YilOyTuruVeIlceCozulur()
    {
        var k = DosyaKimligi.Coz(@"C:\data\raw\2024_yerel_belediye-meclisi_esenyurt.xlsx");

        Assert.Equal("2024_yerel_belediye-meclisi_esenyurt.xlsx", k.DosyaAdi);
        Assert.Equal(new DateOnly(2024, 3, 31), k.SecimTarihi);
        Assert.Equal(OyTuru.BelediyeMeclisi, k.OyTuru);
        Assert.False(k.KontrolDosyasiMi);
        Assert.Equal("34-ESENYURT", k.Ilce.Kod);
        Assert.Equal(34, k.Ilce.Il.Kod);
    }

    [Theory]
    [InlineData("2009_yerel_belediye-baskani_esenyurt.xlsx", 2009, 3, 29)]
    [InlineData("2014_yerel_buyuksehir-baskani_esenyurt.xlsx", 2014, 3, 30)]
    [InlineData("2019_yerel_belediye-meclisi_esenyurt.xlsx", 2019, 3, 31)]
    public void SecimTarihi_YildanBulunur(string ad, int yil, int ay, int gun)
    {
        Assert.Equal(new DateOnly(yil, ay, gun), DosyaKimligi.Coz(ad).SecimTarihi);
    }

    [Fact]
    public void IstanbulIlceleri_KontrolDosyasidir()
    {
        var k = DosyaKimligi.Coz("2024_yerel_belediye-meclisi_istanbul-ilceler.xlsx");

        Assert.True(k.KontrolDosyasiMi);
        Assert.Throws<InvalidOperationException>(() => k.Ilce);
    }

    [Theory]
    [InlineData("2024_belediye-meclisi_esenyurt.xlsx")]     // "yerel" yok
    [InlineData("2024_yerel_belediye-meclisi_esenyurt.xls")] // uzantı yanlış
    [InlineData("2024_yerel_Belediye-Meclisi_esenyurt.xlsx")] // büyük harf
    [InlineData("2010_yerel_belediye-meclisi_esenyurt.xlsx")] // o yıl yerel seçim yok
    [InlineData("2024_yerel_muhtar_esenyurt.xlsx")]          // oy türü tanımsız
    [InlineData("2024_yerel_belediye-meclisi_kadikoy.xlsx")] // ilçe tanımsız
    public void HataliAd_HataVerir(string ad)
    {
        Assert.Throws<YuklemeHatasi>(() => DosyaKimligi.Coz(ad));
    }
}
