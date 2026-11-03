using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// Veritabanının kendi koruduğu kurallar. Domain sınıfları bu hataları zaten engeller; buradaki testler
/// kodu atlayan bir yazmanın (ham SQL, ileride başka bir araç) da reddedildiğini gösterir.
/// </summary>
public class VeritabaniKuraliTestleri(TestVeritabani veritabani) : VeritabaniTesti(veritabani)
{
    [Fact]
    public async Task BekleyenMigrationYok()
    {
        Assert.Empty(await Db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task SandikDuzeyindeSandikBos_Reddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        var hata = await SqlReddedilir($"""
            INSERT INTO "Sonuclar" ("SecimId", "OyTuru", "Duzey", "IlId", "IlceId", "MahalleKoyId", "SandikId", "PartiId", "AdayId", "Oy")
            VALUES ({o.Secim.Id}, 'BelediyeMeclisi', 'Sandik', {o.Il.Id}, {o.Ilce.Id}, {o.Mahalle.Id}, NULL, {o.Dsp.Id}, NULL, 5)
            """);

        Assert.Equal(PostgresErrorCodes.CheckViolation, hata.SqlState);
        Assert.Equal("CK_Sonuclar_Duzey", hata.ConstraintName);
    }

    [Fact]
    public async Task PartisiDeAdayiDaOlmayanSonuc_Reddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        var hata = await SqlReddedilir($"""
            INSERT INTO "Sonuclar" ("SecimId", "OyTuru", "Duzey", "IlId", "IlceId", "MahalleKoyId", "SandikId", "PartiId", "AdayId", "Oy")
            VALUES ({o.Secim.Id}, 'BelediyeMeclisi', 'Ilce', {o.Il.Id}, {o.Ilce.Id}, NULL, NULL, NULL, NULL, 5)
            """);

        Assert.Equal(PostgresErrorCodes.CheckViolation, hata.SqlState);
        Assert.Equal("CK_Sonuclar_PartiYaDaAday", hata.ConstraintName);
    }

    [Fact]
    public async Task NegatifOy_Reddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        var hata = await SqlReddedilir($"""
            INSERT INTO "Sonuclar" ("SecimId", "OyTuru", "Duzey", "IlId", "IlceId", "MahalleKoyId", "SandikId", "PartiId", "AdayId", "Oy")
            VALUES ({o.Secim.Id}, 'BelediyeMeclisi', 'Ilce', {o.Il.Id}, {o.Ilce.Id}, NULL, NULL, {o.Dsp.Id}, NULL, -1)
            """);

        Assert.Equal(PostgresErrorCodes.CheckViolation, hata.SqlState);
        Assert.Equal("CK_Sonuclar_Oy", hata.ConstraintName);
    }

    [Fact]
    public async Task KendiListesindeListePartisi_Reddedilir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        var hata = await SqlReddedilir($"""
            INSERT INTO "DspKatilimlari" ("SecimId", "OyTuru", "IlId", "IlceId", "Durum", "ListePartiId")
            VALUES ({o.Secim.Id}, 'BelediyeMeclisi', {o.Il.Id}, {o.Ilce.Id}, 'KendiListesi', {o.Diger.Id})
            """);

        Assert.Equal(PostgresErrorCodes.CheckViolation, hata.SqlState);
        Assert.Equal("CK_DspKatilimlari_ListePartisi", hata.ConstraintName);
    }

    [Fact]
    public async Task IlceleriOlanIl_Silinemez()
    {
        // Silme bağlı kayıtlara yayılmaz: önce ilçeler açıkça silinmeli.
        var o = await DbOrnekVeri.HazirlaAsync(Db);

        var hata = await Assert.ThrowsAsync<PostgresException>(() =>
            Db.Iller.Where(i => i.Id == o.Il.Id).ExecuteDeleteAsync());

        // ON DELETE RESTRICT ihlali 23001 kodunu verir (NO ACTION olsaydı 23503 olurdu).
        Assert.Equal(PostgresErrorCodes.RestrictViolation, hata.SqlState);
    }

    [Fact]
    public async Task OyTuru_MetinOlarakSaklanir()
    {
        var o = await DbOrnekVeri.HazirlaAsync(Db);
        var sonuc = Sonuc.PartiOyu(o.Secim, OyTuru.BuyuksehirBaskani, Bolge.IlDuzeyinde(o.Il), o.Dsp, 7);
        Db.Add(sonuc);
        await Db.SaveChangesAsync();

        var ham = await Db.Database
            .SqlQuery<string>($"""SELECT "OyTuru" AS "Value" FROM "Sonuclar" WHERE "Id" = {sonuc.Id}""")
            .SingleAsync();

        Assert.Equal("BuyuksehirBaskani", ham);
    }

    [Fact]
    public async Task MahalleAdlari_TurkceAlfabeyeGoreSiralanir()
    {
        // Veritabanı Türkçe ICU sıralamasıyla kurulu: C < Ç, I < İ, O < Ö, S < Ş, U < Ü.
        // Harf koduna göre sıralasaydı Ç, İ, Ö, Ş, Ü en sona düşerdi.
        var o = await DbOrnekVeri.HazirlaAsync(Db);
        string[] beklenen = ["CUMHURİYET", "ÇINAR", "IŞIK", "İNÖNÜ", "OKUL", "ÖRNEK", "SAHİL", "ŞEHİT", "UÇAR", "ÜMİT"];
        var kod = 999100;
        foreach (var ad in beklenen.Reverse())
            Db.Add(new MahalleKoy { Kod = kod++, Ad = ad, Ilce = o.Ilce });
        await Db.SaveChangesAsync();

        var sirali = await Db.MahalleKoyler
            .Where(m => m.IlceId == o.Ilce.Id && m.Id != o.Mahalle.Id)
            .OrderBy(m => m.Ad)
            .Select(m => m.Ad)
            .ToListAsync();

        Assert.Equal(beklenen, sirali);
    }
}
