using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Tests.ImporterTestleri;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// KarneServisi: karne satırlarına sınır durumu ve mahalle detayı.
/// 2009: A ve B (meclis payları %50, %50). 2014: C kuruldu; A payı %25 (−%50, sınırı değişti), B payı %40 (−%20, şüpheli).
/// 2014 belediye başkanı dosyasında DSP sütunu yok (pusulada değil).
/// </summary>
public sealed class KarneServisiTestleri : VeritabaniTesti, IDisposable
{
    private const int A = 999001, B = 999002, C = 999003;
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-karne-" + Guid.NewGuid().ToString("N"));

    public KarneServisiTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(Path.Combine(_klasor, "raw"));

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    private async Task<(KarneServisi Servis, IlceSecenegi Ilce)> HazirlaAsync()
    {
        var yukleyici = new Yukleyici(Db, BaslikTanimlari.Yukle(), Path.Combine(_klasor, "arsiv"));
        async Task Yukle(string ad, object?[] partiler, params object?[][] satirlar)
        {
            var yol = Path.Combine(_klasor, "raw", ad);
            object?[][] tablo = [["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", .. partiler], .. satirlar];
            using (var akis = YskDosyaOkuyucuTestleri.Dosya(tablo)) File.WriteAllBytes(yol, akis.ToArray());
            await yukleyici.YukleAsync(yol);
        }

        // Meclis oranları: 2009 A %10, B %5; 2014 A %5, B %6,25, C %10 (14/140).
        await Yukle("2009_yerel_belediye-meclisi_esenyurt.xlsx", ["AK PARTİ", "DSP"], [A, "A MAH.", 1001, 90, 10], [B, "B MAH.", 1002, 95, 5]);
        await Yukle("2014_yerel_belediye-meclisi_esenyurt.xlsx", ["AK PARTİ", "DSP"],
            [A, "A MAH.", 1001, 95, 5], [B, "B MAH.", 1002, 150, 10], [C, "C MAH.", 1003, 126, 14]);
        await Yukle("2014_yerel_belediye-baskani_esenyurt.xlsx", ["AK PARTİ", "CHP"],
            [A, "A MAH.", 1001, 60, 40], [B, "B MAH.", 1002, 100, 60], [C, "C MAH.", 1003, 100, 40]);

        var servis = new KarneServisi(Db);
        return (servis, (await servis.IlceAsync("34-ESENYURT"))!);
    }

    [Fact]
    public async Task Secenekler_VerisiOlanlar()
    {
        var (servis, ilce) = await HazirlaAsync();

        Assert.Contains(await servis.IlcelerAsync(), i => i.Kod == "34-ESENYURT" && i.IlAdi == "İSTANBUL");
        var secimler = await servis.SecimlerAsync(ilce.Id);
        Assert.Equal([2014, 2009], secimler.Select(s => s.Yil));
        Assert.Equal([OyTuru.BelediyeMeclisi, OyTuru.BelediyeBaskani], await servis.OyTurleriAsync(ilce.Id, secimler[0].Id));
        Assert.Equal([OyTuru.BelediyeMeclisi], await servis.OyTurleriAsync(ilce.Id, secimler[1].Id));
        Assert.Null(await servis.IlceAsync("34-YOK"));
    }

    [Fact]
    public async Task Karne_SinirDurumuVeSupheliUyarisi()
    {
        var (servis, ilce) = await HazirlaAsync();
        var s2014 = (await servis.SecimlerAsync(ilce.Id))[0];

        var karne = await servis.KarneAsync(ilce, s2014, OyTuru.BelediyeMeclisi);

        Assert.Equal([C, B, A], karne.Satirlar.Select(s => s.BirimKod)); // DSP oranı azalan
        var (a, b, c) = (karne.Satirlar[2], karne.Satirlar[1], karne.Satirlar[0]);
        Assert.Equal((SinirDurumu.SiniriDegisti, 2014, false), (a.Sinir!.Sonuc, a.DonemBaslangici, a.Uyari));
        Assert.Equal((SinirDurumu.Supheli, 2009, true), (b.Sinir!.Sonuc, b.DonemBaslangici, b.Uyari));
        Assert.Equal([2014], b.Supheliler.Select(x => x.Yil));
        Assert.Equal(-0.2m, b.Sinir.PayDegisimi);
        Assert.Equal((SinirDurumu.Yeni, 2014), (c.Sinir!.Sonuc, c.DonemBaslangici));
        Assert.Equal(6.25m, b.DspOrani);
    }

    [Fact]
    public async Task Karne_PusuladaDegilse_OyVeOranBos()
    {
        var (servis, ilce) = await HazirlaAsync();
        var s2014 = (await servis.SecimlerAsync(ilce.Id))[0];

        var karne = await servis.KarneAsync(ilce, s2014, OyTuru.BelediyeBaskani);

        Assert.All(karne.Satirlar, s => Assert.Equal((false, (long?)null, (decimal?)null), (s.PusuladaMi, s.DspOyu, s.DspOrani)));
        Assert.All(karne.Satirlar, s => Assert.NotNull(s.Sinir)); // sınır durumu oy türünden bağımsız
    }

    [Fact]
    public async Task Mahalle_SecimlerArasiOranVeSinir()
    {
        var (servis, ilce) = await HazirlaAsync();

        var d = (await servis.MahalleAsync(ilce, B))!;

        Assert.Equal("B MAH.", d.BirimAdi);
        Assert.Equal([(2014, OyTuru.BelediyeMeclisi), (2014, OyTuru.BelediyeBaskani), (2009, OyTuru.BelediyeMeclisi)],
            d.Satirlar.Select(s => (s.Yil, s.OyTuru)));
        var m2014 = d.Satirlar[0];
        Assert.Equal((2009, 1.25m), (m2014.OncekiYil!.Value, m2014.PuanFarki!.Value));
        Assert.Equal(SinirDurumu.Supheli, m2014.Sinir!.Sonuc);
        Assert.Null(d.Satirlar[1].DspOrani);
        Assert.Equal(SinirDurumu.Sabit, d.Satirlar[2].Sinir!.Sonuc);
        Assert.Equal(2009, d.Sinir!.DonemBaslangici);
        Assert.True(d.Sinir.SupheliVar);

        Assert.Null(await servis.MahalleAsync(ilce, 123));
    }
}
