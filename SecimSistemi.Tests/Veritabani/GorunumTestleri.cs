using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Tests.ImporterTestleri;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// Görünümler (MahalleKarnesi, SecimDegisimi, AdayEtkisi) ve erime servisi, küçük bir veri setiyle:
/// iki mahalle (A = 999001, B = 999002), 2009 ve 2014 yerel seçimleri. 2014 belediye başkanı dosyasında DSP sütunu yok.
/// </summary>
public sealed class GorunumTestleri : VeritabaniTesti, IDisposable
{
    private const int A = 999001, B = 999002;
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-gorunum-" + Guid.NewGuid().ToString("N"));
    private int _ilceId;

    public GorunumTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(Path.Combine(_klasor, "raw"));

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    private static readonly object?[] Kimlik = ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No"];

    private async Task VeriYukleAsync()
    {
        var yukleyici = new Yukleyici(Db, BaslikTanimlari.Yukle(), Path.Combine(_klasor, "arsiv"));
        async Task Yukle(string ad, object?[] basliklar, object?[] a, object?[] b)
        {
            var yol = Path.Combine(_klasor, "raw", ad);
            using (var akis = YskDosyaOkuyucuTestleri.Dosya([[.. Kimlik, .. basliklar], [A, "BİRİNCİ MAH.", 1001, .. a], [B, "İKİNCİ MAH.", 1002, .. b]]))
                File.WriteAllBytes(yol, akis.ToArray());
            await yukleyici.YukleAsync(yol);
        }

        // A: meclis %10, başkan %15. B: meclis %5, başkan %5.
        await Yukle("2009_yerel_belediye-meclisi_esenyurt.xlsx", ["AK PARTİ", "CHP", "DSP"], [60, 30, 10], [150, 40, 10]);
        await Yukle("2009_yerel_belediye-baskani_esenyurt.xlsx", ["AK PARTİ", "CHP", "DSP"], [55, 30, 15], [160, 30, 10]);
        // 2014: A meclis 5/200 = %2,5; B 10/400 = %2,5. Başkanlıkta DSP yok.
        await Yukle("2014_yerel_belediye-meclisi_esenyurt.xlsx", ["AK PARTİ", "CHP", "DSP"], [140, 55, 5], [250, 140, 10]);
        await Yukle("2014_yerel_belediye-baskani_esenyurt.xlsx", ["AK PARTİ", "CHP"], [150, 50], [300, 100]);

        _ilceId = (await Db.Ilceler.SingleAsync(i => i.Kod == "34-ESENYURT")).Id;
    }

    [Fact]
    public async Task Karne_BirimBasinaToplamlarVeOran()
    {
        await VeriYukleAsync();

        var k = await Db.MahalleKarnesi.SingleAsync(x => x.BirimKod == A && x.Yil == 2009 && x.OyTuru == OyTuru.BelediyeMeclisi);

        Assert.Equal((100L, (long?)10L, true, 1), (k.ToplamOy, k.DspOyu, k.PusuladaMi, k.SandikSayisi));
        Assert.Equal(10m, k.DspOrani);
        Assert.Equal(SecimTuru.Yerel, k.SecimTuru);
        Assert.Equal(8, await Db.MahalleKarnesi.CountAsync()); // 2 birim × 4 dosya
    }

    [Fact]
    public async Task Karne_PusuladaOlmayanSecimdeBos()
    {
        await VeriYukleAsync();

        var k = await Db.MahalleKarnesi.SingleAsync(x => x.BirimKod == A && x.Yil == 2014 && x.OyTuru == OyTuru.BelediyeBaskani);

        Assert.False(k.PusuladaMi);
        Assert.Null(k.DspOyu);
        Assert.Null(k.DspOrani);
        Assert.Equal(200, k.ToplamOy);
    }

    [Fact]
    public async Task Degisim_PuanVeOyFarki()
    {
        await VeriYukleAsync();

        var d = await Db.SecimDegisimi.SingleAsync(x => x.BirimKod == A && x.OyTuru == OyTuru.BelediyeMeclisi);

        Assert.Equal((2009, 2014), (d.OncekiYil, d.Yil));
        Assert.Equal((10m, 2.5m, -7.5m), (d.OncekiOran!.Value, d.Oran!.Value, d.PuanFarki!.Value));
        Assert.Equal((10L, 5L, -5L), (d.OncekiDspOyu!.Value, d.DspOyu!.Value, d.OyFarki!.Value));

        var baskan = await Db.SecimDegisimi.SingleAsync(x => x.BirimKod == A && x.OyTuru == OyTuru.BelediyeBaskani);
        Assert.Null(baskan.PuanFarki); // 2014'te pusulada değil
    }

    [Fact]
    public async Task AdayEtkisi_BaskanlikEksiMeclis()
    {
        await VeriYukleAsync();

        var e2009 = await Db.AdayEtkisi.SingleAsync(x => x.BirimKod == A && x.Yil == 2009);
        Assert.Equal(OyTuru.BelediyeBaskani, e2009.BaskanlikTuru);
        Assert.Equal((10m, 15m, 5m), (e2009.MeclisOrani!.Value, e2009.BaskanlikOrani!.Value, e2009.AdayEtkisiPuan!.Value));
        Assert.Equal(5, e2009.OyFarki);

        var e2014 = await Db.AdayEtkisi.SingleAsync(x => x.BirimKod == A && x.Yil == 2014);
        Assert.Equal(2.5m, e2014.MeclisOrani);
        Assert.Null(e2014.BaskanlikOrani);
        Assert.Null(e2014.AdayEtkisiPuan);
    }

    [Fact]
    public async Task ErimeServisi_GorunumdenHesaplar()
    {
        await VeriYukleAsync();
        var servis = new ErimeRaporuServisi(Db);

        var meclis = await servis.HazirlaAsync(_ilceId, OyTuru.BelediyeMeclisi);

        Assert.Equal(2014, meclis.SonYil);
        // A: beklenen 0,10 × 200 = 20, kayıp 15, korunma 0,25. B: beklenen 0,05 × 400 = 20, kayıp 10, korunma 0,5.
        Assert.Equal([A, B], meclis.Satirlar.Select(s => s.BirimKod));
        var a = meclis.Satirlar[0];
        Assert.Equal((20m, 15m, 0.25m), (a.BeklenenOy!.Value, a.KayipOy!.Value, a.KorunmaOrani!.Value));
        Assert.Equal([A], meclis.KaleKodlari); // ceil(2 × 0,2) = 1
        Assert.Equal([10m, 2.5m], meclis.KaleOrtalamalari.Select(k => k.Oran!.Value));

        // Başkanlıkta 2014'te DSP pusulada değil: son seçim 2009.
        var baskan = await servis.HazirlaAsync(_ilceId, OyTuru.BelediyeBaskani);
        Assert.Equal(2009, baskan.SonYil);
        Assert.Null(baskan.KaleOrtalamalari.Single(k => k.Yil == 2014).Oran);
    }
}
