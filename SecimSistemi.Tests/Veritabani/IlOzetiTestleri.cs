using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Tests.ImporterTestleri;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// İl özeti: yüklü ilçeler kendiliğinden görünür, ilçe düzeyi erime ve yüklü ilçelerin toplamı.
/// Esenyurt 2009: DSP 10/100 (%10), 2024: 5/200 (%2,5). Şişli 2009: 4/100 (%4), 2024: 6/100 (%6).
/// </summary>
public sealed class IlOzetiTestleri : VeritabaniTesti, IDisposable
{
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-il-" + Guid.NewGuid().ToString("N"));

    public IlOzetiTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(Path.Combine(_klasor, "raw"));

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    private async Task Yukle(string ad, params object?[][] satirlar)
    {
        var yukleyici = new Yukleyici(Db, BaslikTanimlari.Yukle(), Path.Combine(_klasor, "arsiv"));
        var yol = Path.Combine(_klasor, "raw", ad);
        object?[][] tablo = [["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "DSP"], .. satirlar];
        using (var akis = YskDosyaOkuyucuTestleri.Dosya(tablo)) File.WriteAllBytes(yol, akis.ToArray());
        await yukleyici.YukleAsync(yol);
    }

    [Fact]
    public async Task YukluIlcelerKendiligindenGorunur_ToplamVeSiralama()
    {
        await Yukle("2009_yerel_belediye-meclisi_esenyurt.xlsx", [998001, "A MAH.", 1001, 90, 10]);
        await Yukle("2024_yerel_belediye-meclisi_esenyurt.xlsx", [998001, "A MAH.", 1001, 195, 5]);
        await Yukle("2009_yerel_belediye-meclisi_sisli.xlsx", [998101, "B MAH.", 1001, 96, 4]);
        await Yukle("2024_yerel_belediye-meclisi_sisli.xlsx", [998101, "B MAH.", 1001, 94, 6]);

        var o = await IlOzetiHazirlayici.HazirlaAsync(Db, new IlOzetiSorgusu("İSTANBUL", null));

        Assert.Equal("İSTANBUL", o.Il);
        Assert.Equal(OyTuru.BelediyeMeclisi, o.OyTuru);
        // Varsayılan sıra: kayıp oy azalan. Esenyurt: beklenen %10 × 200 = 20, kayıp 15. Şişli: beklenen 4, gerçek 6 → kayıp −2.
        Assert.Equal(["ESENYURT", "ŞİŞLİ"], o.Ilceler.Select(x => x.Ilce.Ad));
        var e = o.Ilceler[0];
        Assert.Equal((2024, 2.5m, 10m, -7.5m, 15m), (e.Son!.Yil, e.Oran!.Value, e.Taban!.Oran!.Value, e.Puan!.Value, e.Kayip!.Value));
        // Toplam: 2009 14/200 = %7; 2024 11/300; beklenen %7 × 300 = 21, kayıp 10.
        Assert.Equal((7m, 10m), (o.ToplamTaban!.Oran!.Value, o.ToplamSon!.KayipOy!.Value));
        Assert.Equal("İSTANBUL: 39 ilçeden yüklü olan 2 ilçe (ESENYURT, ŞİŞLİ). Toplam satırı yalnızca yüklü ilçelerin toplamıdır, il geneli değildir.",
            o.KapsamUyarisi);

        var dsp = await IlOzetiHazirlayici.HazirlaAsync(Db, new IlOzetiSorgusu("İSTANBUL", null, "dsp-orani", null));
        Assert.Equal(["ŞİŞLİ", "ESENYURT"], dsp.Ilceler.Select(x => x.Ilce.Ad)); // %6 > %2,5
    }

    private static IlceOzeti Oz(string ad, decimal? oran, decimal? kayip) =>
        new(new IlceSecenegi(0, $"34-{ad}", ad, "İSTANBUL"), null,
            new IlceErimeSatiri(2024, 100, 1, oran, null, null, null, kayip), 1, 0);

    [Fact]
    public void Sirala_BoslarSonda_EsitlikteAd()
    {
        IlceOzeti[] l = [Oz("ŞİŞLİ", 1m, null), Oz("ADALAR", 2m, 10m), Oz("BAKIRKÖY", 1m, 10m), Oz("ESENYURT", null, 5m)];

        Assert.Equal(["ADALAR", "BAKIRKÖY", "ESENYURT", "ŞİŞLİ"], IlOzetiHazirlayici.Sirala(l, IlSiraAlani.Kayip, true).Select(x => x.Ilce.Ad));
        Assert.Equal(["BAKIRKÖY", "ŞİŞLİ", "ADALAR", "ESENYURT"], IlOzetiHazirlayici.Sirala(l, IlSiraAlani.DspOrani, false).Select(x => x.Ilce.Ad));
        Assert.Equal(["ADALAR", "BAKIRKÖY", "ESENYURT", "ŞİŞLİ"], IlOzetiHazirlayici.Sirala(l, IlSiraAlani.Ad, false).Select(x => x.Ilce.Ad));
    }

    [Fact]
    public void Belge_ToplamSatiriUyariVeGrafik()
    {
        var o = new IlOzeti(["İSTANBUL"], "İSTANBUL", [OyTuru.BelediyeMeclisi], OyTuru.BelediyeMeclisi,
            [Oz("ESENYURT", 0.21m, 3564m) with { SupheliMahalle = 3 }], null,
            new IlceErimeSatiri(2024, 463488, 975, 0.21m, -0.77m, 0.21m, 4539m, 3564m), IlSiraAlani.Kayip, true,
            new IlOzetiSorgusu("İSTANBUL", "belediye-meclisi"));

        var b = RaporBelgeleri.IlOzeti(o, KaynakListesi.Bos, new DateTime(2026, 11, 25));

        Assert.StartsWith("İSTANBUL: 39 ilçeden yüklü olan 1 ilçe (ESENYURT).", b.Uyari);
        var t = Assert.Single(b.Bolumler);
        Assert.Equal(SatirTuru.Uyari, t.Satirlar[0].Tur);            // şüpheli mahalle var
        Assert.Equal(SatirTuru.Toplam, t.Satirlar[^1].Tur);
        Assert.Equal("Yüklü ilçelerin toplamı", t.Satirlar[^1].Degerler[1]);
        Assert.IsType<CubukGrafigi>(Assert.Single(t.GrafikListesi));
        Assert.Equal("il_İSTANBUL_belediye-meclisi", b.DosyaAdi);
    }
}
