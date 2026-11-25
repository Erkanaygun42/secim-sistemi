using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Tests.ImporterTestleri;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>Meclis hesaplayıcısının girdisi: ilçenin belediye meclisi parti oyları (sandık toplamı); başkanlık oyları karışmaz.</summary>
public sealed class MeclisServisiTestleri : VeritabaniTesti, IDisposable
{
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-meclis-" + Guid.NewGuid().ToString("N"));

    public MeclisServisiTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(Path.Combine(_klasor, "raw"));

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    [Fact]
    public async Task Listeler_MeclisOylarininToplami_CoktanAza()
    {
        var yukleyici = new Yukleyici(Db, BaslikTanimlari.Yukle(), Path.Combine(_klasor, "arsiv"));
        async Task Yukle(string ad, object?[] partiler, params object?[][] satirlar)
        {
            var yol = Path.Combine(_klasor, "raw", ad);
            object?[][] tablo = [["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", .. partiler], .. satirlar];
            using (var akis = YskDosyaOkuyucuTestleri.Dosya(tablo)) File.WriteAllBytes(yol, akis.ToArray());
            await yukleyici.YukleAsync(yol);
        }
        await Yukle("2014_yerel_belediye-meclisi_esenyurt.xlsx", ["AK PARTİ", "CHP", "DSP"],
            [999101, "A MAH.", 1001, 300, 150, 30], [999102, "B MAH.", 1002, 200, 250, 20]);
        await Yukle("2014_yerel_belediye-baskani_esenyurt.xlsx", ["AK PARTİ", "CHP"],
            [999101, "A MAH.", 1001, 1000, 1000], [999102, "B MAH.", 1002, 1000, 1000]);

        var karne = new KarneServisi(Db);
        var ilce = (await karne.IlceAsync("34-ESENYURT"))!;
        var secim = (await karne.SecimlerAsync(ilce.Id)).Single(s => s.Yil == 2014);

        var listeler = await new MeclisServisi(Db).ListelerAsync(ilce.Id, secim.Id);

        Assert.Equal([("AK PARTİ", 500L), ("CHP", 400L), ("DSP", 50L)], listeler.Select(l => (l.Ad, l.Oy)));
        Assert.All(listeler, l => Assert.False(l.Bagimsiz));
    }
}

/// <summary>Tanimlar/meclis-uye-sayilari.csv biçimi.</summary>
public class MeclisUyeSayilariTestleri
{
    private const string Baslik = "ilce;yil;uye_sayisi;durum;kaynak";

    [Fact]
    public void Coz_YorumlarAtlanir_DurumOkunur()
    {
        var k = MeclisUyeSayilari.Coz(["# açıklama", Baslik, "34-ESENYURT;2024;45;dogrulanmadi;kullanıcı varsayımı", "34-ESENYURT;2019;45;dogrulandi;sonuc.ysk.gov.tr"]);

        Assert.Equal([new MeclisUyeSayisi("34-ESENYURT", 2024, 45, false, "kullanıcı varsayımı"),
                      new MeclisUyeSayisi("34-ESENYURT", 2019, 45, true, "sonuc.ysk.gov.tr")], k);
    }

    [Theory]
    [InlineData("34-ESENYURT;2024;40;dogrulandi;x", "2972 m.5/b")]       // kanunda olmayan üye sayısı
    [InlineData("34-ESENYURT;2024;45;evet;x", "durum")]
    [InlineData("34-ESENYURT;2024;45;dogrulandi;", "kaynak")]
    [InlineData("34-ESENYURT;2024;45;dogrulandi", "5 alan")]
    public void GecersizSatir_SatirNumarasiylaHata(string satir, string beklenen)
    {
        var h = Assert.Throws<FormatException>(() => MeclisUyeSayilari.Coz([Baslik, satir]));
        Assert.Contains(":2:", h.Message);
        Assert.Contains(beklenen, h.Message);
    }

    [Fact]
    public void AyniIlceVeYilIkiKez_Hata() =>
        Assert.Throws<FormatException>(() => MeclisUyeSayilari.Coz([Baslik, "34-ESENYURT;2024;45;dogrulandi;a", "34-ESENYURT;2024;55;dogrulandi;b"]));

    [Fact]
    public void DepodakiDosya_Gecerli_Esenyurt2024DogrulanmadiOlarak45()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SecimSistemi.Importer", "Tanimlar", MeclisUyeSayilari.DosyaAdi);

        var k = MeclisUyeSayilari.Oku(Path.GetFullPath(yol));

        var e = Assert.Single(k, x => x.IlceKod == "34-ESENYURT" && x.Yil == 2024);
        Assert.Equal((45, false), (e.UyeSayisi, e.Dogrulandi)); // resmî kaynak gelene kadar doğrulanmadı
        Assert.Equal(5, MeclisHesabi.KontenjanSayisi(e.UyeSayisi));
    }
}
