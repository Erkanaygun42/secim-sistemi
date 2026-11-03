using Npgsql;
using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>Aynı dosya iki kez yüklendiğinde veritabanı çift kaydı reddetmeli.</summary>
public class CiftKayitTestleri(TestVeritabani veritabani) : VeritabaniTesti(veritabani)
{
    [Fact]
    public async Task AyniSandikSonucu_IkinciKezReddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);
        var bolge = Bolge.SandikDuzeyinde(o.Sandik);
        Db.Add(Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeMeclisi, bolge, o.Dsp, 42));
        await Db.SaveChangesAsync();

        Db.Add(Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeMeclisi, bolge, o.Dsp, 42));
        var hata = await KaydetmeReddedilir();

        Assert.Equal(PostgresErrorCodes.UniqueViolation, hata.SqlState);
        Assert.Equal("IX_Sonuclar_Tekil", hata.ConstraintName);
    }

    [Fact]
    public async Task AyniIlceSonucu_BosSutunlaraRagmenIkinciKezReddedilir()
    {
        // İlçe düzeyinde MahalleKoyId ve SandikId boştur. PostgreSQL varsayılanında boşlar birbirinden farklı
        // sayıldığı için bu kayıt ikinci kez yazılabilirdi; NULLS NOT DISTINCT ayarı bunu engeller.
        var o = await DbOrnekVeri.HazirlaAsync(Db);
        var bolge = Bolge.IlceDuzeyinde(o.Ilce);
        Db.Add(Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeBaskani, bolge, o.Dsp, 1000));
        await Db.SaveChangesAsync();

        Db.Add(Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeBaskani, bolge, o.Dsp, 1000));
        var hata = await KaydetmeReddedilir();

        Assert.Equal(PostgresErrorCodes.UniqueViolation, hata.SqlState);
        Assert.Equal("IX_Sonuclar_Tekil", hata.ConstraintName);
    }

    [Fact]
    public async Task AyniSandiktaFarkliParti_VeFarkliOyTuru_Kaydedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);
        var bolge = Bolge.SandikDuzeyinde(o.Sandik);

        Db.AddRange(
            Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeMeclisi, bolge, o.Dsp, 42),
            Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeMeclisi, bolge, o.Diger, 17),
            Sonuc.PartiOyu(o.Secim, OyTuru.BelediyeBaskani, bolge, o.Dsp, 40));

        Assert.Equal(3, await Db.SaveChangesAsync());
    }

    [Fact]
    public async Task AyniSandik_IkinciKezReddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        Db.Add(new Sandik { No = o.Sandik.No, Secim = o.Secim, MahalleKoy = o.Mahalle });
        var hata = await KaydetmeReddedilir();

        Assert.Equal(PostgresErrorCodes.UniqueViolation, hata.SqlState);
    }

    [Fact]
    public async Task AyniMahalleKodu_IkinciKezReddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        Db.Add(new MahalleKoy { Kod = o.Mahalle.Kod, Ad = "BAŞKA AD", Ilce = o.Ilce });
        var hata = await KaydetmeReddedilir();

        Assert.Equal(PostgresErrorCodes.UniqueViolation, hata.SqlState);
    }

    [Fact]
    public async Task IlGeneliDspKatilimi_IkinciKezReddedilir()
    {
        // İl geneli kayıtta IlceId boştur; boşlar eşit sayıldığı için ikinci kayıt reddedilir.
        var o = await DbOrnekVeri.HazirlaAsync(Db);
        Db.Add(DspKatilimi.Olustur(o.Secim, OyTuru.BuyuksehirBaskani, o.Il, null, DspDurumu.Girmedi));
        await Db.SaveChangesAsync();

        Db.Add(DspKatilimi.Olustur(o.Secim, OyTuru.BuyuksehirBaskani, o.Il, null, DspDurumu.KendiListesi));
        var hata = await KaydetmeReddedilir();

        Assert.Equal(PostgresErrorCodes.UniqueViolation, hata.SqlState);
    }
}
