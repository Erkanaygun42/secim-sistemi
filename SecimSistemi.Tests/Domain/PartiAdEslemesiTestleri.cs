using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Domain;

public class PartiAdEslemesiTestleri
{
    [Theory]
    [InlineData("DEM Parti", "DEM PARTİ")]
    [InlineData("  İyi   Parti ", "İYİ PARTİ")]
    [InlineData("dsp", "DSP")]
    [InlineData("Saadet\tPartisi", "SAADET PARTİSİ")]
    public void Normallestir_TurkceBuyukHarfVeTekBosluk(string ham, string beklenen)
    {
        Assert.Equal(beklenen, PartiAdEslemesi.Normallestir(ham));
    }

    [Fact]
    public void GecerliMi_UclarDahil()
    {
        var esleme = new PartiAdEslemesi
        {
            HamAd = "İP",
            GecerliBaslangic = new DateOnly(2010, 1, 1),
            GecerliBitis = new DateOnly(2017, 12, 31),
        };

        Assert.True(esleme.GecerliMi(new DateOnly(2010, 1, 1)));
        Assert.True(esleme.GecerliMi(new DateOnly(2014, 3, 30)));
        Assert.True(esleme.GecerliMi(new DateOnly(2017, 12, 31)));
        Assert.False(esleme.GecerliMi(new DateOnly(2009, 12, 31)));
        Assert.False(esleme.GecerliMi(new DateOnly(2018, 6, 24)));
    }

    [Fact]
    public void GecerliMi_SinirsizUclarHerTarihteGecerli()
    {
        var esleme = new PartiAdEslemesi { HamAd = "DSP" };

        Assert.True(esleme.GecerliMi(new DateOnly(1991, 10, 20)));
        Assert.True(esleme.GecerliMi(new DateOnly(2024, 3, 31)));
    }
}
