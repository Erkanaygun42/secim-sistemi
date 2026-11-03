using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;
using SecimSistemi.Importer;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Tests.ImporterTestleri;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// Yükleyicinin veritabanı davranışı. Dosyalar geçici bir klasörde üretilir; veritabanı yazmaları testin
/// transaction'ı içinde kalır ve test bitince geri alınır.
/// </summary>
public sealed class YukleyiciTestleri : VeritabaniTesti, IDisposable
{
    private const string MeclisDosyasi = "2024_yerel_belediye-meclisi_esenyurt.xlsx";
    private const string BuyuksehirDosyasi = "2024_yerel_buyuksehir-baskani_esenyurt.xlsx";
    private static readonly DateOnly Secim2024 = new(2024, 3, 31);
    private static readonly BaslikTanimlari Tanimlar = BaslikTanimlari.Yukle();

    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-yukleyici-" + Guid.NewGuid().ToString("N"));
    private string RawKlasoru => Path.Combine(_klasor, "raw");
    private string ArsivKlasoru => Path.Combine(_klasor, "arsiv");

    public YukleyiciTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(RawKlasoru);

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    private Yukleyici Yukleyici() => new(Db, Tanimlar, ArsivKlasoru);

    /// <summary>İki mahalle, üç sandık; FIRAT GÜNDÜZ 2024 belediye meclisinde tanımlı bağımsız aday.</summary>
    private static object?[][] MeclisSatirlari(int ilkSandikDsp = 3) =>
    [
        ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "CHP", "DSP", "FIRAT GÜNDÜZ"],
        [999001, "BİRİNCİ MAH.", 1001, 91, 60, ilkSandikDsp, 1],
        [999001, "BİRİNCİ MAH.", 1002, 80, 61, 1, 0],
        [999002, "İKİNCİ MAH.", 1003, 70, 50, 2, 0],
    ];

    private string DosyaYaz(string ad, object?[][] satirlar)
    {
        var yol = Path.Combine(RawKlasoru, ad);
        using var akis = YskDosyaOkuyucuTestleri.Dosya(satirlar);
        File.WriteAllBytes(yol, akis.ToArray());
        return yol;
    }

    private async Task<int> Sayi<T>() where T : class => await Db.Set<T>().CountAsync();

    [Fact]
    public async Task IlkYukleme_HerSeyiYazar()
    {
        var yol = DosyaYaz(MeclisDosyasi, MeclisSatirlari());

        var ozet = await Yukleyici().YukleAsync(yol);

        Assert.Equal(YuklemeIslemi.Yuklendi, ozet.Islem);
        Assert.Equal((3, 3, 2, 419L, 6L), (ozet.SatirSayisi, ozet.SandikSayisi, ozet.MahalleSayisi, ozet.ToplamOy, ozet.DspOyu));
        Assert.Equal(KontrolSonucu.DosyaYok, ozet.Kontrol);

        var secim = await Db.Secimler.SingleAsync(s => s.Tarih == Secim2024);
        Assert.Equal(SecimTuru.Yerel, secim.Tur);
        var ilce = await Db.Ilceler.Include(i => i.Il).SingleAsync(i => i.Kod == "34-ESENYURT");
        Assert.Equal((34, "ESENYURT"), (ilce.Il.Kod, ilce.Ad));

        var mahalleler = await Db.MahalleKoyler.Where(m => m.IlceId == ilce.Id).OrderBy(m => m.Kod).ToListAsync();
        Assert.Equal([999001, 999002], mahalleler.Select(m => m.Kod));
        Assert.All(mahalleler, m => Assert.Equal(MahalleKoyTuru.Mahalle, m.Tur));

        Assert.Equal(3, await Db.Sandiklar.CountAsync(s => s.SecimId == secim.Id));
        Assert.Equal(12, await Db.Sonuclar.CountAsync(s => s.SecimId == secim.Id)); // 3 sandık × 4 sütun, sıfırlar dahil
        Assert.Equal(6, await Db.Sonuclar.Where(s => s.Parti!.KisaAd == "DSP").SumAsync(s => s.Oy));

        var aday = await Db.Adaylar.SingleAsync();
        Assert.Equal(("FIRAT GÜNDÜZ", OyTuru.BelediyeMeclisi, (int?)ilce.Id), (aday.AdSoyad, aday.OyTuru, aday.IlceId));
        Assert.Null(aday.PartiId);

        var dsp = await Db.DspKatilimlari.SingleAsync();
        Assert.Equal((DspDurumu.KendiListesi, "Cumhur İttifakı", (int?)ilce.Id), (dsp.Durum, dsp.IttifakAdi, dsp.IlceId));

        // Partiler ve başlık eşlemeleri basliklar.csv'den eşitlendi.
        Assert.Equal(Tanimlar.Partiler.Count, await Sayi<Parti>());
        Assert.Equal(Tanimlar.PartiEslemeleri.Count, await Sayi<PartiAdEslemesi>());

        var kayit = await Db.Yuklemeler.SingleAsync();
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(yol)));
        Assert.Equal((MeclisDosyasi, sha, YuklemeDurumu.Aktif, 419L), (kayit.DosyaAdi, kayit.Sha256, kayit.Durum, kayit.ToplamOy));

        // Ham dosya değiştirilmeden, SHA256 adıyla arşivlendi.
        Assert.Equal(File.ReadAllBytes(yol), File.ReadAllBytes(Path.Combine(ArsivKlasoru, sha + ".xlsx")));
    }

    [Fact]
    public async Task AyniDosyaIkinciKez_Atlanir()
    {
        var yol = DosyaYaz(MeclisDosyasi, MeclisSatirlari());
        await Yukleyici().YukleAsync(yol);

        var ikinci = await Yukleyici().YukleAsync(yol);

        Assert.Equal(YuklemeIslemi.Atlandi, ikinci.Islem);
        Assert.Contains("atlandı", Assert.Single(ikinci.Uyarilar));
        Assert.Equal(12, await Sayi<Sonuc>());
        Assert.Equal(1, await Sayi<Yukleme>());
    }

    [Fact]
    public async Task AyniIcerikFarkliAdla_YineAtlanir()
    {
        var yol = DosyaYaz(MeclisDosyasi, MeclisSatirlari());
        await Yukleyici().YukleAsync(yol);
        var kopya = Path.Combine(_klasor, "kopya");
        Directory.CreateDirectory(kopya);
        var kopyaYolu = Path.Combine(kopya, MeclisDosyasi);
        File.Copy(yol, kopyaYolu);

        Assert.Equal(YuklemeIslemi.Atlandi, (await Yukleyici().YukleAsync(kopyaYolu)).Islem);
    }

    [Fact]
    public async Task AyniSecimFarkliDosya_YenileOlmadanDurur()
    {
        await Yukleyici().YukleAsync(DosyaYaz(MeclisDosyasi, MeclisSatirlari(ilkSandikDsp: 3)));
        var degisik = DosyaYaz(MeclisDosyasi, MeclisSatirlari(ilkSandikDsp: 30));

        var hata = await Assert.ThrowsAsync<YuklemeHatasi>(() => Yukleyici().YukleAsync(degisik));

        Assert.Contains("--yenile", hata.Message);
        Assert.Equal(6, await Db.Sonuclar.Where(s => s.Parti!.KisaAd == "DSP").SumAsync(s => s.Oy));
        Assert.Equal(1, await Sayi<Yukleme>());
    }

    [Fact]
    public async Task Yenile_EskiSonuclarinYerineGecer()
    {
        await Yukleyici().YukleAsync(DosyaYaz(MeclisDosyasi, MeclisSatirlari(ilkSandikDsp: 3)));
        var degisik = DosyaYaz(MeclisDosyasi, MeclisSatirlari(ilkSandikDsp: 30));

        var ozet = await Yukleyici().YukleAsync(degisik, yenile: true);

        Assert.Equal(YuklemeIslemi.Yenilendi, ozet.Islem);
        Assert.Equal(12, await Sayi<Sonuc>());
        Assert.Equal(33, await Db.Sonuclar.Where(s => s.Parti!.KisaAd == "DSP").SumAsync(s => s.Oy));
        var kayitlar = await Db.Yuklemeler.OrderBy(y => y.Id).Select(y => y.Durum).ToListAsync();
        Assert.Equal([YuklemeDurumu.Yenilendi, YuklemeDurumu.Aktif], kayitlar);
    }

    [Fact]
    public async Task Yenile_ArtikKullanilmayanSandikVeAdayiSiler()
    {
        await Yukleyici().YukleAsync(DosyaYaz(MeclisDosyasi, MeclisSatirlari()));
        // Yeni dosyada 1003 numaralı sandık ve bağımsız aday sütunu yok.
        var kisa = DosyaYaz(MeclisDosyasi,
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "CHP", "DSP"],
            [999001, "BİRİNCİ MAH.", 1001, 91, 60, 3],
            [999001, "BİRİNCİ MAH.", 1002, 80, 61, 1],
        ]);

        await Yukleyici().YukleAsync(kisa, yenile: true);

        Assert.Equal([1001, 1002], await Db.Sandiklar.OrderBy(s => s.No).Select(s => s.No).ToListAsync());
        Assert.Equal(0, await Sayi<Aday>());
        Assert.Equal(2, await Sayi<MahalleKoy>()); // mahalleler kalıcıdır, silinmez
    }

    [Fact]
    public async Task BilinmeyenBaslik_HicbirSeyYazmaz()
    {
        var yol = DosyaYaz(MeclisDosyasi,
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "YENİ PARTİ", "DSP"],
            [999001, "BİRİNCİ MAH.", 1001, 91, 60, 3],
        ]);

        var hata = await Assert.ThrowsAsync<BilinmeyenBaslikHatasi>(() => Yukleyici().YukleAsync(yol));

        Assert.Contains("'YENİ PARTİ'", hata.Message);
        Assert.Equal(0, await Sayi<Secim>());
        Assert.Equal(0, await Sayi<Parti>());
        Assert.Equal(0, await Sayi<Yukleme>());
        Assert.False(Directory.Exists(ArsivKlasoru));
    }

    [Fact]
    public async Task UcOyTuru_AyniSandiklariPaylasir()
    {
        await Yukleyici().YukleAsync(DosyaYaz(MeclisDosyasi, MeclisSatirlari()));
        await Yukleyici().YukleAsync(DosyaYaz(BuyuksehirDosyasi,
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "CHP", "AK PARTİ", "DSP"],
            [999001, "BİRİNCİ MAH.", 1001, 70, 80, 2],
            [999001, "BİRİNCİ MAH.", 1002, 60, 81, 1],
            [999002, "İKİNCİ MAH.", 1003, 50, 71, 0],
        ]));

        Assert.Equal(3, await Sayi<Sandik>());
        Assert.Equal(2, await Sayi<MahalleKoy>());
        Assert.Equal(12 + 9, await Sayi<Sonuc>());
        Assert.Equal(2, await Sayi<Yukleme>());
    }

    [Fact]
    public async Task Buyuksehir_AdayVeDspIlGenelindeTutulur()
    {
        var yol = DosyaYaz(BuyuksehirDosyasi,
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "CHP", "AK PARTİ", "FATMA RAGİBE KANIKURU LOĞOĞLU"],
            [999001, "BİRİNCİ MAH.", 1001, 70, 80, 0],
        ]);

        var ozet = await Yukleyici().YukleAsync(yol);

        Assert.Null(ozet.DspOyu);
        var aday = await Db.Adaylar.SingleAsync();
        Assert.Null(aday.IlceId);
        var dsp = await Db.DspKatilimlari.SingleAsync();
        Assert.Equal((DspDurumu.Girmedi, (int?)null, (string?)null), (dsp.Durum, dsp.IlceId, dsp.IttifakAdi));
    }

    [Fact]
    public async Task KontrolDosyasi_DogrudanYuklenmez()
    {
        var yol = DosyaYaz("2024_yerel_belediye-meclisi_istanbul-ilceler.xlsx", MeclisSatirlari());

        await Assert.ThrowsAsync<YuklemeHatasi>(() => Yukleyici().YukleAsync(yol));
    }

    [Fact]
    public async Task KontrolDosyasiVarsa_KontrolYapilmadiYazilir()
    {
        DosyaYaz("2024_yerel_belediye-meclisi_istanbul-ilceler.xlsx", MeclisSatirlari());

        var ozet = await Yukleyici().YukleAsync(DosyaYaz(MeclisDosyasi, MeclisSatirlari()));

        Assert.Equal(KontrolSonucu.Yapilmadi, ozet.Kontrol);
        Assert.Contains(ozet.Uyarilar, u => u.Contains("okuyucusu henüz yazılmadı"));
    }

    [Fact]
    public async Task AyniOyluSatirlar_UyariOlurAmaSilinmez()
    {
        var ozet = await Yukleyici().YukleAsync(DosyaYaz(MeclisDosyasi,
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "CHP", "DSP"],
            [999001, "BİRİNCİ MAH.", 1001, 91, 60, 3],
            [999001, "BİRİNCİ MAH.", 1002, 91, 60, 3],
        ]));

        Assert.Contains(ozet.Uyarilar, u => u.Contains("birebir aynı 1 satır grubu") && u.Contains("[2,3]"));
        Assert.Equal(2, await Sayi<Sandik>());
    }

    [Fact]
    public async Task MahalleMiKoyMuAnlasilmazsa_Durur()
    {
        var yol = DosyaYaz(MeclisDosyasi,
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ"],
            [999001, "BİLİNMEYEN BİRİM", 1001, 91],
        ]);

        var hata = await Assert.ThrowsAsync<YuklemeHatasi>(() => Yukleyici().YukleAsync(yol));
        Assert.Contains("BİLİNMEYEN BİRİM", hata.Message);
        Assert.Equal(0, await Sayi<Secim>());
    }
}
