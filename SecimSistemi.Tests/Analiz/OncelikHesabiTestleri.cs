using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Analiz;

/// <summary>Öncelik listesi: dönem başına ayrı, kayıp oy ve büyüklüğe göre sıralı; her satırda tek cümle.</summary>
public class OncelikHesabiTestleri
{
    private static BirimYilDurumu D(int yil, SinirDurumu s) => new(yil, s, s, null, null, null);

    private static readonly SinirAnaliziSonucu Sinir = new(SinirEsikleri.Varsayilan, [2009, 2019, 2024],
    [
        new BirimDonemi(1, "A", 2009, 2024, [D(2009, SinirDurumu.Sabit), D(2019, SinirDurumu.Supheli), D(2024, SinirDurumu.Sabit)]),
        new BirimDonemi(2, "B", 2009, 2024, [D(2009, SinirDurumu.Sabit), D(2019, SinirDurumu.Sabit), D(2024, SinirDurumu.Sabit)]),
        new BirimDonemi(3, "C", 2009, 2024, [D(2009, SinirDurumu.Sabit), D(2019, SinirDurumu.Sabit), D(2024, SinirDurumu.Sabit)]),
        new BirimDonemi(4, "D", 2019, 2024, [D(2009, SinirDurumu.SonradanKuruldu), D(2019, SinirDurumu.Yeni), D(2024, SinirDurumu.Sabit)]),
    ]);

    private static ErimeSatiri S(int kod, int taban, decimal tabanOrani, decimal sonOrani, decimal? kayip, long sonToplam) =>
        new(kod, $"M{kod} MAH.", taban, tabanOrani, 1000, 10, sonOrani, sonToplam, 5, sonOrani - tabanOrani,
            sonOrani / tabanOrani, 10m, kayip, false, null);

    private static IReadOnlyList<OncelikListesi> Hesapla(params ErimeSatiri[] satirlar)
    {
        var g = GrupHesabi.Hesapla(new ErimeRaporu(OyTuru.BelediyeMeclisi, 2009, 2024, satirlar, [], []),
            new Dictionary<int, decimal?> { [2009] = 1.0m, [2019] = 0.3m, [2024] = 0.2m }, GrupEsikleri.Varsayilan);
        return OncelikHesabi.Hesapla(g, Sinir);
    }

    [Fact]
    public void DonemBasinaAyriListe_KayipOyaSonraBuyukluge()
    {
        var l = Hesapla(
            S(1, 2009, 3.85m, 0.32m, 434m, 12_274),
            S(2, 2009, 0.68m, 0.27m, 75m, 18_441),
            S(3, 2009, 0.70m, 0.35m, 75m, 14_958),   // kayıp eşit: büyük olan (2) önce
            S(4, 2019, 0.51m, 0.06m, 62m, 13_971));

        Assert.Equal(["2009–2024", "2019–2024"], l.Select(x => x.Donem));
        Assert.Equal([1, 2, 3], l[0].Satirlar.Select(s => s.Grup.BirimKod));
        Assert.Equal([1, 2, 3], l[0].Satirlar.Select(s => s.Sira));
        Assert.Equal(4, Assert.Single(l[1].Satirlar).Grup.BirimKod);
        Assert.Equal(1, l[1].Satirlar[0].Sira);              // sıra her dönemde 1'den
    }

    [Fact]
    public void KazancSonda_BuyuklukSirasiIlceninTamaminda()
    {
        var l = Hesapla(
            S(1, 2009, 0.5m, 1.0m, -30m, 5_000),      // kazanç: sona
            S(2, 2009, 2.0m, 1.0m, 20m, 1_000),
            S(4, 2019, 0.5m, 0.2m, 10m, 9_000));

        Assert.Equal([2, 1], l[0].Satirlar.Select(s => s.Grup.BirimKod));
        Assert.Equal((3, 3), (l[0].Satirlar[0].BuyuklukSirasi!.Value, l[0].Satirlar[0].MahalleSayisi)); // 9.000 > 5.000 > 1.000
        Assert.Equal(1, l[1].Satirlar[0].BuyuklukSirasi);   // D, başka dönemde ama ilçenin en büyüğü
    }

    [Fact]
    public void Cumle_ErozyonKayipBuyuklukVeSinirUyarisi()
    {
        var a = Hesapla(S(1, 2009, 3.85m, 0.32m, 434m, 12_274), S(2, 2009, 0.68m, 0.27m, 75m, 18_441))[0].Satirlar[0];

        Assert.Equal("M1 MAH.: 2009'da %3,85 ile ilçe oranının (%1,00) üstündeydi ama geriledi (2024'te %0,32); " +
                     "tabanını koruyabilseydi 434 oy daha alırdı (ilçenin 2. büyük mahallesi, 12.274 oy); " +
                     "⚠ 2019 seçiminde sınır şüphesi var.", OncelikCumlesi.Yaz(a));
    }

    [Theory]
    [InlineData(2.0, 1.9, 3, "üstündeydi ve gücünü korudu (2024'te %1,90); tabanını koruyabilseydi 3 oy daha alırdı")]
    [InlineData(2.0, 3.0, -40, "üstündeydi ve gücünü artırdı (2024'te %3,00); tabanının 40 oy üstünde")]
    [InlineData(0.5, 1.0, -25, "altındaydı ama yükseldi (2024'te %1,00); tabanının 25 oy üstünde")]
    [InlineData(0.5, 0.2, 20, "altındaydı ve daha da geriledi (2024'te %0,20); tabanını koruyabilseydi 20 oy daha alırdı")]
    [InlineData(0.5, 0.48, 2, "altındaydı ve değişmedi (2024'te %0,48); tabanını koruyabilseydi 2 oy daha alırdı")]
    public void Cumle_GrubaGoreSeyir(double taban, double son, double kayip, string beklenen)
    {
        var s = Hesapla(S(2, 2009, (decimal)taban, (decimal)son, (decimal)kayip, 1000))[0].Satirlar[0];

        var cumle = OncelikCumlesi.Yaz(s);

        Assert.Contains(beklenen, cumle);
        Assert.EndsWith(".", cumle);
        Assert.DoesNotMatch(@"[a-zıüöçşğ)]\. \p{Lu}", cumle); // tek cümle ("1. büyük" sıra sayısı cümle sonu değil)
    }

    [Fact]
    public void Cumle_EnBuyukMahalle_YuvarlamadaEsitOranlarUcOndalik()
    {
        // Taban 2019 %0,305, ilçe 2019 %0,3 (iki ondalıkta ikisi de %0,30/%0,31 olabilir): eşit görünürse üç ondalık.
        var g = GrupHesabi.Hesapla(new ErimeRaporu(OyTuru.BelediyeMeclisi, 2009, 2024, [S(4, 2019, 0.2996m, 0.07m, 34m, 13_837)], [], []),
            new Dictionary<int, decimal?> { [2019] = 0.3049m, [2024] = 0.2m }, GrupEsikleri.Varsayilan);

        var cumle = OncelikCumlesi.Yaz(OncelikHesabi.Hesapla(g, Sinir)[0].Satirlar[0]);

        Assert.Contains("2019'da %0,300 ile ilçe oranının (%0,305) altındaydı", cumle);
        Assert.Contains("(ilçenin en büyük mahallesi, 13.837 oy)", cumle);
    }

    [Fact]
    public void Cumle_GrupYoksaNedeni()
    {
        var g = GrupHesabi.Hesapla(new ErimeRaporu(OyTuru.BelediyeMeclisi, 2009, 2024,
            [new ErimeSatiri(2, "M2 MAH.", 2009, null, 1000, null, 0.2m, 1000, 2, null, null, null, null, false, "taban seçiminde (2009) DSP pusulada değil")],
            [], []), new Dictionary<int, decimal?> { [2009] = 1m, [2024] = 0.2m }, GrupEsikleri.Varsayilan);

        var s = OncelikHesabi.Hesapla(g, Sinir)[0].Satirlar[0];

        Assert.Equal("M2 MAH.: grup hesaplanamadı (taban seçiminde (2009) DSP pusulada değil).", OncelikCumlesi.Yaz(s));
    }

    [Theory]
    [InlineData(2009, "2009'da")]
    [InlineData(2014, "2014'te")]
    [InlineData(2019, "2019'da")]
    [InlineData(2024, "2024'te")]
    [InlineData(2002, "2002'de")]
    [InlineData(2003, "2003'te")]
    [InlineData(2006, "2006'da")]
    [InlineData(2010, "2010'da")]
    [InlineData(2020, "2020'de")]
    [InlineData(2040, "2040'ta")]
    [InlineData(2070, "2070'te")]
    [InlineData(2000, "2000'de")]
    [InlineData(1999, "1999'da")]
    [InlineData(1994, "1994'te")]
    public void Bulunma_OkunusaGoreEk(int yil, string beklenen) => Assert.Equal(beklenen, Metinler.Bulunma(yil));
}
