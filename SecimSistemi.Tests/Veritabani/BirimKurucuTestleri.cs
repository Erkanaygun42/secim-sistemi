using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Importer.Mahalleler;
using SecimSistemi.Tests.ImporterTestleri;

namespace SecimSistemi.Tests.Veritabani;

public sealed class BirimKurucuTestleri : VeritabaniTesti, IDisposable
{
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-birim-" + Guid.NewGuid().ToString("N"));

    public BirimKurucuTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(Path.Combine(_klasor, "raw"));

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    private async Task<Ilce> YukleAsync()
    {
        var yol = Path.Combine(_klasor, "raw", "2024_yerel_belediye-meclisi_esenyurt.xlsx");
        using (var akis = YskDosyaOkuyucuTestleri.Dosya(
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "DSP"],
            [999001, "BİRİNCİ MAH.", 1001, 90, 3],
            [999002, "İKİNCİ MAH.", 1002, 80, 2],
            [999003, "ÜÇÜNCÜ MAH.", 1003, 70, 1],
        ]))
            File.WriteAllBytes(yol, akis.ToArray());
        await new Yukleyici(Db, BaslikTanimlari.Yukle(), Path.Combine(_klasor, "arsiv")).YukleAsync(yol);
        return await Db.Ilceler.SingleAsync(i => i.Kod == "34-ESENYURT");
    }

    [Fact]
    public async Task Yukleme_HerMahalleIcinBirimKurar()
    {
        var ilce = await YukleAsync();

        var birimler = await Db.KarsilastirmaBirimleri.Include(b => b.Uyeler).Where(b => b.IlceId == ilce.Id).OrderBy(b => b.Kod).ToListAsync();

        Assert.Equal([999001, 999002, 999003], birimler.Select(b => b.Kod));
        Assert.All(birimler, b => Assert.Single(b.Uyeler));
    }

    [Fact]
    public async Task OnayliEsleme_IkiMahalleyiTekBirimYapar()
    {
        var ilce = await YukleAsync();
        var m = await Db.MahalleKoyler.Where(x => x.IlceId == ilce.Id).ToDictionaryAsync(x => x.Kod);
        Db.MahalleKoyEslemeleri.Add(new MahalleKoyEslemesi
        {
            EskiMahalleKoyId = m[999001].Id, YeniMahalleKoyId = m[999003].Id, Tur = EslemeTuru.Bolunme,
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        await BirimKurucu.YenidenKurAsync(Db, ilce.Id);

        var birimler = await Db.KarsilastirmaBirimleri.Include(b => b.Uyeler).OrderBy(b => b.Kod).ToListAsync();
        Assert.Equal([999001, 999002], birimler.Select(b => b.Kod));
        Assert.Equal("BİRİNCİ MAH. + ÜÇÜNCÜ MAH.", birimler[0].Ad);
        Assert.Equal(2, birimler[0].Uyeler.Count);
        Assert.Equal(3, await Db.KarsilastirmaBirimiUyeleri.CountAsync()); // her mahalle tam bir birimde
    }
}
