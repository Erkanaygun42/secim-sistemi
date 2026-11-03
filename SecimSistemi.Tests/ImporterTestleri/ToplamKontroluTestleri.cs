using SecimSistemi.Importer.Kontrol;

namespace SecimSistemi.Tests.ImporterTestleri;

public class ToplamKontroluTestleri
{
    private static readonly Kalem Akp = Kalem.Parti("AK PARTİ");
    private static readonly Kalem Chp = Kalem.Parti("CHP");
    private static readonly Kalem Dsp = Kalem.Parti("DSP");
    private static readonly Kalem Aday = Kalem.Aday("AYŞE YILMAZ");

    private static Dictionary<Kalem, long> Oylar(params (Kalem Kalem, long Oy)[] oylar) =>
        oylar.ToDictionary(x => x.Kalem, x => x.Oy);

    [Fact]
    public void AyniToplamlar_Tutarli()
    {
        var r = ToplamKontrolu.Karsilastir(
            Oylar((Akp, 100), (Chp, 80), (Dsp, 5)),
            Oylar((Dsp, 5), (Akp, 100), (Chp, 80)),
            ilanGecerliOy: 185);

        Assert.True(r.TutarliMi);
        Assert.Empty(r.Farklar);
        Assert.Equal(185, r.HesaplananToplam);
    }

    [Fact]
    public void FarkliOy_FarkOlarakRaporlanir()
    {
        var r = ToplamKontrolu.Karsilastir(Oylar((Akp, 100), (Dsp, 63)), Oylar((Akp, 100), (Dsp, 60)), null);

        Assert.False(r.TutarliMi);
        var f = Assert.Single(r.Farklar);
        Assert.Equal((Dsp, 63L, 60L, 3L), (f.Kalem, f.Hesaplanan, f.Ilan, f.Fark));
    }

    [Fact]
    public void YalnizcaIlandaOyluKalem_Farktir()
    {
        // Sandık dosyasında sütunu olmayan ama ilçe tablosunda oyu olan parti: sütun kaybolmuş olabilir.
        var r = ToplamKontrolu.Karsilastir(Oylar((Akp, 100)), Oylar((Akp, 100), (Chp, 7)), null);

        var f = Assert.Single(r.Farklar);
        Assert.Equal(Chp, f.Kalem);
        Assert.Null(f.Hesaplanan);
        Assert.Equal(-7, f.Fark);
    }

    [Fact]
    public void YalnizcaSandiklardaOyluKalem_Farktir()
    {
        var r = ToplamKontrolu.Karsilastir(Oylar((Akp, 100), (Aday, 2)), Oylar((Akp, 100)), null);

        var f = Assert.Single(r.Farklar);
        Assert.Equal(Aday, f.Kalem);
        Assert.Null(f.Ilan);
    }

    [Fact]
    public void TekTaraftaSifirOy_FarkDegilBilgidir()
    {
        // İstanbul geneli adaylar ilçe tablosunda 0 oyla yer alabilir.
        var r = ToplamKontrolu.Karsilastir(Oylar((Akp, 100)), Oylar((Akp, 100), (Aday, 0)), null);

        Assert.True(r.TutarliMi);
        Assert.Equal(Aday, Assert.Single(r.TekTaraftaSifir));
    }

    [Fact]
    public void GecerliOyTutmazsa_Tutarsiz()
    {
        var r = ToplamKontrolu.Karsilastir(Oylar((Akp, 100), (Chp, 80)), Oylar((Akp, 100), (Chp, 80)), ilanGecerliOy: 181);

        Assert.Empty(r.Farklar);
        Assert.False(r.GecerliOyTutuyor);
        Assert.False(r.TutarliMi);
    }

    [Fact]
    public void IlanGecerliOyYoksa_GecerliOyKarsilastirilmaz()
    {
        var r = ToplamKontrolu.Karsilastir(Oylar((Akp, 100)), Oylar((Akp, 100)), ilanGecerliOy: null);

        Assert.True(r.GecerliOyTutuyor);
    }

    [Fact]
    public void PartiVeAyniAdliAday_AyriKalemlerdir()
    {
        var r = ToplamKontrolu.Karsilastir(
            Oylar((Kalem.Parti("MİLLET"), 10)),
            Oylar((Kalem.Aday("MİLLET"), 10)),
            null);

        Assert.Equal(2, r.Farklar.Count);
    }

    [Fact]
    public void Farklar_TurkceAlfabeyeGoreSiralanir()
    {
        var r = ToplamKontrolu.Karsilastir(
            Oylar((Kalem.Parti("ZAFER PARTİSİ"), 1), (Kalem.Parti("ÇİFTÇİ"), 1), (Kalem.Parti("CHP"), 1)),
            Oylar(),
            null);

        Assert.Equal(["CHP", "ÇİFTÇİ", "ZAFER PARTİSİ"], r.Farklar.Select(f => f.Kalem.Ad));
    }
}
