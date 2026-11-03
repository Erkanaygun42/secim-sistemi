using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Domain;

public class BolgeTestleri
{
    [Fact]
    public void SandikDuzeyi_ButunUstBolgeleriDoldurur()
    {
        var sandik = OrnekVeri.Sandik(OrnekVeri.Yerel2024());

        var bolge = Bolge.SandikDuzeyinde(sandik);

        Assert.Equal(BolgeDuzeyi.Sandik, bolge.Duzey);
        Assert.Same(sandik, bolge.Sandik);
        Assert.Same(sandik.MahalleKoy, bolge.MahalleKoy);
        Assert.Same(sandik.MahalleKoy.Ilce, bolge.Ilce);
        Assert.Same(sandik.MahalleKoy.Ilce.Il, bolge.Il);
    }

    [Fact]
    public void IlceDuzeyi_AltBolgeleriBosBirakir()
    {
        var ilce = OrnekVeri.Esenyurt();

        var bolge = Bolge.IlceDuzeyinde(ilce);

        Assert.Equal(BolgeDuzeyi.Ilce, bolge.Duzey);
        Assert.Same(ilce.Il, bolge.Il);
        Assert.Null(bolge.MahalleKoy);
        Assert.Null(bolge.Sandik);
    }

    [Fact]
    public void IlcesiYuklenmemisMahalle_HataVerir()
    {
        var mahalle = new MahalleKoy { Kod = 1, Ad = "ÜST BÖLGESİ YOK" };

        Assert.Throws<ArgumentException>(() => Bolge.MahalleKoyDuzeyinde(mahalle));
    }
}
