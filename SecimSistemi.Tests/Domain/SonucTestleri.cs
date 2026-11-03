using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Domain;

public class SonucTestleri
{
    [Fact]
    public void PartiOyu_BolgeSutunlariniDoldurur()
    {
        var secim = OrnekVeri.Yerel2024();
        var sandik = OrnekVeri.Sandik(secim);

        var sonuc = Sonuc.PartiOyu(secim, OyTuru.BelediyeMeclisi, Bolge.SandikDuzeyinde(sandik), OrnekVeri.Dsp(), 42);

        Assert.Equal(BolgeDuzeyi.Sandik, sonuc.Duzey);
        Assert.Equal(34, sonuc.IlId);
        Assert.Equal(1, sonuc.IlceId);
        Assert.Equal(10, sonuc.MahalleKoyId);
        Assert.Equal(100, sonuc.SandikId);
        Assert.Equal(42, sonuc.Oy);
    }

    [Fact]
    public void BaskaSeciminSandigi_HataVerir()
    {
        var sandik2019 = OrnekVeri.Sandik(OrnekVeri.Yerel2019());

        Assert.Throws<ArgumentException>(() =>
            Sonuc.PartiOyu(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, Bolge.SandikDuzeyinde(sandik2019), OrnekVeri.Dsp(), 1));
    }

    [Fact]
    public void NegatifOy_HataVerir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Sonuc.PartiOyu(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, Bolge.IlceDuzeyinde(OrnekVeri.Esenyurt()), OrnekVeri.Dsp(), -1));
    }

    [Fact]
    public void AdayOyu_FarkliOyTurundeHataVerir()
    {
        var secim = OrnekVeri.Yerel2024();
        var ilce = OrnekVeri.Esenyurt();
        var aday = new Aday { AdSoyad = "BAĞIMSIZ ADAY", Secim = secim, SecimId = secim.Id, OyTuru = OyTuru.BelediyeBaskani, Il = ilce.Il, Ilce = ilce };

        Assert.Throws<ArgumentException>(() =>
            Sonuc.AdayOyu(secim, OyTuru.BelediyeMeclisi, Bolge.IlceDuzeyinde(ilce), aday, 5));
    }

    [Fact]
    public void AdayOyu_BaskaSeciminAdayindaHataVerir()
    {
        var secim2019 = OrnekVeri.Yerel2019();
        var ilce = OrnekVeri.Esenyurt();
        var aday = new Aday { AdSoyad = "BAĞIMSIZ ADAY", Secim = secim2019, SecimId = secim2019.Id, OyTuru = OyTuru.BelediyeBaskani, Il = ilce.Il, Ilce = ilce };

        Assert.Throws<ArgumentException>(() =>
            Sonuc.AdayOyu(OrnekVeri.Yerel2024(), OyTuru.BelediyeBaskani, Bolge.IlceDuzeyinde(ilce), aday, 5));
    }

    [Fact]
    public void OyOzeti_NegatifDegerdeHataVerir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OyOzeti.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, Bolge.IlceDuzeyinde(OrnekVeri.Esenyurt()), gecersizOy: -3));
    }

    [Fact]
    public void OyOzeti_AlanlarBosOlabilir()
    {
        var ozet = OyOzeti.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, Bolge.IlceDuzeyinde(OrnekVeri.Esenyurt()));

        Assert.Null(ozet.KayitliSecmen);
        Assert.Null(ozet.GecerliOy);
    }
}
