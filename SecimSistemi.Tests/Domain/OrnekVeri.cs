using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Domain;

/// <summary>Testlerde kullanılan küçük bir İstanbul/Esenyurt örneği.</summary>
internal static class OrnekVeri
{
    public static Il Istanbul() => new() { Id = 34, Kod = 34, Ad = "İSTANBUL" };

    public static Ilce Esenyurt(Il? il = null)
    {
        il ??= Istanbul();
        return new Ilce { Id = 1, Kod = "ESENYURT", Ad = "ESENYURT", Il = il, IlId = il.Id };
    }

    public static MahalleKoy Mahalle(Ilce? ilce = null)
    {
        ilce ??= Esenyurt();
        return new MahalleKoy { Id = 10, Kod = 12345, Ad = "ÖRNEK MAHALLESİ", Ilce = ilce, IlceId = ilce.Id };
    }

    public static Secim Yerel2024() => new() { Id = 1, Tarih = new DateOnly(2024, 3, 31), Tur = SecimTuru.Yerel, Ad = "2024 Yerel" };

    public static Secim Yerel2019() => new() { Id = 2, Tarih = new DateOnly(2019, 3, 31), Tur = SecimTuru.Yerel, Ad = "2019 Yerel" };

    public static Sandik Sandik(Secim secim, MahalleKoy? mahalle = null)
    {
        mahalle ??= Mahalle();
        return new Sandik { Id = 100, No = 1001, Secim = secim, SecimId = secim.Id, MahalleKoy = mahalle, MahalleKoyId = mahalle.Id };
    }

    public static Parti Dsp() => new() { Id = 1, KisaAd = "DSP" };
}
