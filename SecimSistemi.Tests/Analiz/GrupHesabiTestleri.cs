using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

/// <summary>Mahalle grupları: güç (dönem başında, ilçeye göre), eğilim (gürültü eşiğiyle), dört grup.</summary>
public class GrupHesabiTestleri
{
    private static readonly GrupEsikleri E = GrupEsikleri.Varsayilan; // 10 oy, %30

    /// <param name="kayip">Kayıp oy (beklenen − gerçek); eksi değer kazançtır.</param>
    private static ErimeSatiri S(int kod, int taban, decimal? tabanOrani, decimal? sonOrani, decimal? kayip, string? not = null) =>
        new(kod, $"M{kod}", taban, tabanOrani, 1000, 10, sonOrani, 1000, 5, sonOrani - tabanOrani,
            tabanOrani > 0 ? sonOrani / tabanOrani : null, 10m, kayip, false, not);

    private static GrupSonucu Hesapla(params ErimeSatiri[] satirlar) =>
        GrupHesabi.Hesapla(new ErimeRaporu(OyTuru.BelediyeMeclisi, 2009, 2024, satirlar, [], []),
            new Dictionary<int, decimal?> { [2009] = 1.0m, [2019] = 0.3m, [2024] = 0.2m }, E);

    [Fact]
    public void DortGrup()
    {
        var s = Hesapla(
            S(1, 2009, 2.0m, 0.5m, 40m),    // güçlü (2,0 ≥ 1,0), %75 düştü, 40 oy → Erozyon
            S(2, 2009, 2.0m, 1.9m, 3m),     // güçlü, %5 / 3 oy → değişmedi → Kale
            S(3, 2009, 0.5m, 1.0m, -25m),   // zayıf, %100 arttı, 25 oy → Yükselen
            S(4, 2009, 0.5m, 0.2m, 20m),    // zayıf, kaybetti → Zayıf
            S(5, 2019, 0.6m, 0.9m, -12m));  // 2019 tabanı: ilçe 0,3 → güçlü, kazandı → Kale

        Assert.Equal([MahalleGrubu.Erozyon, MahalleGrubu.Kale, MahalleGrubu.Yukselen, MahalleGrubu.Zayif, MahalleGrubu.Kale],
            s.Satirlar.Select(x => x.Grup!.Value));
        Assert.Equal([Egilim.Kaybetti, Egilim.Degismedi, Egilim.Kazandi, Egilim.Kaybetti, Egilim.Kazandi],
            s.Satirlar.Select(x => x.Egilim!.Value));
        Assert.Equal(1, s.DegismeyenSayisi);
        Assert.Equal(2, s.Sayi(MahalleGrubu.Kale));
    }

    [Fact]
    public void Guc_IlceninTabanYilindakiOraninaGore_EsitlikGuclu()
    {
        var s = Hesapla(S(1, 2009, 1.0m, 1.0m, 0m), S(2, 2019, 0.29m, 0.29m, 0m));

        Assert.True(s.Satirlar[0].Gucluydu);   // 1,0 = ilçe 2009 (1,0)
        Assert.False(s.Satirlar[1].Gucluydu);  // 0,29 < ilçe 2019 (0,3)
        Assert.Equal(0m, s.Satirlar[0].DonemBasindakiGuc);
        Assert.Equal(0.8m, s.Satirlar[0].BugunkuGuc);   // 1,0 − ilçe 2024 (0,2)
    }

    [Theory]
    [InlineData(-10, -0.30, Egilim.Kaybetti)]   // ikisi de tam eşikte: değişti
    [InlineData(-9.99, -0.90, Egilim.Degismedi)] // oy yetmiyor
    [InlineData(-500, -0.29, Egilim.Degismedi)]  // oran yetmiyor
    [InlineData(10, 0.30, Egilim.Kazandi)]
    [InlineData(0, 0, Egilim.Degismedi)]
    public void Egilim_IkiEsigideGecmeli(double oy, double goreli, Egilim beklenen) =>
        Assert.Equal(beklenen, GrupHesabi.EgilimBul((decimal)oy, (decimal)goreli, E));

    [Fact]
    public void GoreliEsik_ZayifVeGucluIlcedeAyniIsiGorur()
    {
        // Aynı %50 düşüş: zayıf ilçede 0,4 → 0,2 (0,2 puan), güçlü ilçede 8 → 4 (4 puan). İkisi de "kaybetti".
        Assert.Equal(Egilim.Kaybetti, GrupHesabi.EgilimBul(-20m, 0.2m / 0.4m - 1, E));
        Assert.Equal(Egilim.Kaybetti, GrupHesabi.EgilimBul(-400m, 4m / 8m - 1, E));
    }

    [Fact]
    public void TabanOraniSifir_YalnizcaOyEsigi()
    {
        var s = Hesapla(S(1, 2009, 0m, 0.4m, -40m, not: "taban seçiminde (2009) oran 0; korunma oranı hesaplanamaz"),
                        S(2, 2009, 0m, 0.05m, -5m));

        Assert.Null(s.Satirlar[0].GoreliDegisim);
        Assert.Equal((MahalleGrubu.Yukselen, (string?)null), (s.Satirlar[0].Grup!.Value, s.Satirlar[0].Not));
        Assert.Equal(MahalleGrubu.Zayif, s.Satirlar[1].Grup);   // 5 oy < 10: değişmedi
    }

    [Fact]
    public void PusuladaDegilse_GrupYokNotVar()
    {
        var s = Hesapla(S(1, 2009, null, 0.4m, null, not: "taban seçiminde (2009) DSP pusulada değil"),
                        S(2, 2014, 0.5m, 0.2m, 10m));   // ilçenin 2014 oranı yok

        Assert.Equal((null, "taban seçiminde (2009) DSP pusulada değil"), (s.Satirlar[0].Grup, s.Satirlar[0].Not));
        Assert.Null(s.Satirlar[1].Grup);
        Assert.Equal("ilçenin 2014 oranı yok (DSP pusulada değil)", s.Satirlar[1].Not);
        Assert.Equal(0, s.DegismeyenSayisi);
    }

    [Theory]
    [InlineData(-1, 0.3)]
    [InlineData(10, 1)]
    [InlineData(10, -0.1)]
    public void GecersizEsik(double oy, double goreli) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrupEsikleri((decimal)oy, (decimal)goreli).Dogrula());
}
