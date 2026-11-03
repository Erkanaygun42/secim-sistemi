using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Domain;

public class DspKatilimiTestleri
{
    [Fact]
    public void KendiListesiVeIttifak_Kaydedilir()
    {
        var ilce = OrnekVeri.Esenyurt();

        var katilim = DspKatilimi.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, ilce.Il, ilce,
            DspDurumu.KendiListesi, ittifakAdi: "  Cumhur İttifakı ");

        Assert.Equal(DspDurumu.KendiListesi, katilim.Durum);
        Assert.Equal("Cumhur İttifakı", katilim.IttifakAdi);
        Assert.Null(katilim.ListePartiId);
    }

    [Fact]
    public void BaskaPartininListesi_ListePartisiOlmadanHataVerir()
    {
        Assert.Throws<ArgumentException>(() =>
            DspKatilimi.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, OrnekVeri.Istanbul(), null, DspDurumu.BaskaPartininListesi));
    }

    [Fact]
    public void KendiListesi_ListePartisiVerilirseHataVerir()
    {
        var chp = new Parti { Id = 2, KisaAd = "CHP" };

        Assert.Throws<ArgumentException>(() =>
            DspKatilimi.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, OrnekVeri.Istanbul(), null, DspDurumu.KendiListesi, chp));
    }

    [Fact]
    public void BaskaIlinIlcesi_HataVerir()
    {
        var ankara = new Il { Id = 6, Kod = 6, Ad = "ANKARA" };

        Assert.Throws<ArgumentException>(() =>
            DspKatilimi.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, ankara, OrnekVeri.Esenyurt(), DspDurumu.Girmedi));
    }

    [Fact]
    public void BosIttifakAdi_NullOlarakKaydedilir()
    {
        var katilim = DspKatilimi.Olustur(OrnekVeri.Yerel2024(), OyTuru.BelediyeMeclisi, OrnekVeri.Istanbul(), null,
            DspDurumu.Girmedi, ittifakAdi: "   ");

        Assert.Null(katilim.IttifakAdi);
    }
}
