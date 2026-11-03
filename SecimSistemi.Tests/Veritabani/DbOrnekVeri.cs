using SecimSistemi.Data;
using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// Veritabanı testleri için kaydedilmiş küçük bir bölge zinciri: il › ilçe › mahalle › sandık, bir seçim, iki parti.
/// Kodlar gerçek dışı seçildi (il 99, seçim 2099) ki test veritabanındaki başka verilerle çakışmasın.
/// </summary>
internal sealed record DbOrnekVeri(Il Il, Ilce Ilce, MahalleKoy Mahalle, Sandik Sandik, Secim Secim, Parti Dsp, Parti Diger)
{
    public static async Task<DbOrnekVeri> HazirlaAsync(SecimDbContext db)
    {
        var il = new Il { Kod = 99, Ad = "TEST İLİ" };
        var ilce = new Ilce { Kod = "TEST-01", Ad = "TEST İLÇESİ", Il = il };
        var mahalle = new MahalleKoy { Kod = 999001, Ad = "TEST MAHALLESİ", Ilce = ilce };
        var secim = new Secim { Tarih = new DateOnly(2099, 3, 31), Tur = SecimTuru.Yerel, Ad = "2099 Yerel (test)" };
        var sandik = new Sandik { No = 1001, Secim = secim, MahalleKoy = mahalle };
        var dsp = new Parti { KisaAd = "TEST-DSP" };
        var diger = new Parti { KisaAd = "TEST-DİĞER" };

        db.AddRange(il, ilce, mahalle, secim, sandik, dsp, diger);
        await db.SaveChangesAsync();

        return new DbOrnekVeri(il, ilce, mahalle, sandik, secim, dsp, diger);
    }
}
