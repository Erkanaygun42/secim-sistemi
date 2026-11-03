using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

public class BirimOlusturucuTestleri
{
    private static readonly BirimMahallesi A = new(100, "ARDIÇLI MAH.");
    private static readonly BirimMahallesi B = new(200, "BAĞLARÇEŞME MAH.");
    private static readonly BirimMahallesi C = new(300, "CUMHURİYET MAH.");
    private static readonly BirimMahallesi D = new(400, "ÇINAR MAH.");

    [Fact]
    public void EslemeYoksa_HerMahalleKendiBirimi()
    {
        var birimler = BirimOlusturucu.Olustur([C, A, B], []);

        Assert.Equal([100, 200, 300], birimler.Select(b => b.Kod));
        Assert.All(birimler, b => Assert.Single(b.MahalleKodlari));
        Assert.Equal("ARDIÇLI MAH.", birimler[0].Ad);
    }

    [Fact]
    public void Zincir_TekBirimOlur()
    {
        var birimler = BirimOlusturucu.Olustur([A, B, C, D], [new(100, 200), new(200, 300)]);

        Assert.Equal(2, birimler.Count);
        Assert.Equal([100, 200, 300], birimler[0].MahalleKodlari);
        Assert.Equal([400], birimler[1].MahalleKodlari);
    }

    [Fact]
    public void Bolunme_EskiVeYenilerTekBirim()
    {
        // A bölündü: A'dan B ve C ayrıldı.
        var birim = Assert.Single(BirimOlusturucu.Olustur([A, B, C], [new(100, 200), new(100, 300)]));

        Assert.Equal([100, 200, 300], birim.MahalleKodlari);
    }

    [Fact]
    public void Birlesme_TekBirim()
    {
        // B ve C birleşip D oldu.
        var birimler = BirimOlusturucu.Olustur([A, B, C, D], [new(200, 400), new(300, 400)]);

        Assert.Equal([[100], [200, 300, 400]], birimler.Select(b => b.MahalleKodlari.ToArray()));
    }

    [Fact]
    public void Kod_EnKucukMuhtarlikId_KenarYonundenBagimsiz()
    {
        var bir = BirimOlusturucu.Olustur([A, B, C], [new(300, 200), new(200, 100)]);
        var iki = BirimOlusturucu.Olustur([C, B, A], [new(100, 200), new(300, 100)]);

        Assert.Equal(100, Assert.Single(bir).Kod);
        Assert.Equal(Assert.Single(bir).Ad, Assert.Single(iki).Ad);
    }

    [Fact]
    public void BirimAdi_TurkceAlfabeyleSiralanir()
    {
        var birim = Assert.Single(BirimOlusturucu.Olustur([D, C], [new(300, 400)]));

        Assert.Equal("CUMHURİYET MAH. + ÇINAR MAH.", birim.Ad);
    }

    [Fact]
    public void BaskaIlceyeGidenEsleme_HataVerir()
    {
        Assert.Throws<ArgumentException>(() => BirimOlusturucu.Olustur([A, B], [new(100, 999)]));
    }

    [Fact]
    public void AyniMahalleIkiKez_HataVerir()
    {
        Assert.Throws<ArgumentException>(() => BirimOlusturucu.Olustur([A, A], []));
    }
}
