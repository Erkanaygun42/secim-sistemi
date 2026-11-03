using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

public class ErimeHesabiTestleri
{
    private const OyTuru Meclis = OyTuru.BelediyeMeclisi;

    private static KarneDegeri K(int kod, int yil, long toplam, long? dsp, OyTuru tur = Meclis) =>
        new(kod, $"B{kod}", yil, tur, toplam, dsp, PusuladaMi: dsp is not null);

    private static ErimeRaporu Hesapla(params KarneDegeri[] d) => ErimeHesabi.Hesapla(d, Meclis, ErimeHesabi.Kaleler(d));

    [Fact]
    public void BeklenenVeKayipOy()
    {
        // 2009: %10 (10/100). 2024: toplam 300, DSP 6 → beklenen 0,10 × 300 = 30, kayıp 30 − 6 = 24.
        var s = Assert.Single(Hesapla(K(1, 2009, 100, 10), K(1, 2024, 300, 6)).Satirlar);

        Assert.Equal(10m, s.TabanOrani);
        Assert.Equal(2m, s.SonOrani);
        Assert.Equal(-8m, s.PuanFarki);
        Assert.Equal(0.2m, s.KorunmaOrani);
        Assert.Equal(30m, s.BeklenenOy);
        Assert.Equal(24m, s.KayipOy);
    }

    [Fact]
    public void OyArtsaBile_OranDustuyseKayipVar()
    {
        // Ham oy 10 → 15 arttı, ama toplam üç katına çıktı: beklenen 30, kayıp 15.
        var s = Assert.Single(Hesapla(K(1, 2009, 100, 10), K(1, 2024, 300, 15)).Satirlar);
        Assert.Equal(15m, s.KayipOy);
        Assert.Equal(0.5m, s.KorunmaOrani);
    }

    [Fact]
    public void TabanOraniSifir_KorunmaBos()
    {
        var s = Assert.Single(Hesapla(K(1, 2009, 100, 0), K(1, 2024, 300, 6)).Satirlar);

        Assert.Equal(0m, s.TabanOrani);
        Assert.Null(s.KorunmaOrani);
        Assert.Equal(0m, s.BeklenenOy);   // beklenen 0 × 300
        Assert.Equal(-6m, s.KayipOy);     // DSP tabanın üstünde oy aldı
        Assert.Contains("oranı 0", s.Not);
    }

    [Fact]
    public void SonSecim_PusuladaOlunanEnSonSecimdir()
    {
        // 2024'te DSP pusulada değil: son seçim 2019.
        var r = Hesapla(K(1, 2009, 100, 10), K(1, 2019, 200, 8), K(1, 2024, 300, null));

        Assert.Equal(2019, r.SonYil);
        var s = Assert.Single(r.Satirlar);
        Assert.Equal(200, s.SonToplamOy);
        Assert.Equal(20m, s.BeklenenOy);
    }

    [Fact]
    public void PusuladaOlmayanSecim_SifirDegilBos()
    {
        Assert.Null(ErimeHesabi.Oran(300, null, pusuladaMi: false));
        Assert.Equal(0m, ErimeHesabi.Oran(300, 0, pusuladaMi: true));

        // 2009'da DSP pusulada değilse taban oranı boştur, 0 sayılmaz; hiçbir türev hesaplanmaz.
        var s = Assert.Single(Hesapla(K(1, 2009, 100, null), K(1, 2024, 300, 6)).Satirlar);
        Assert.Null(s.TabanOrani);
        Assert.Null(s.TabanDspOyu);
        Assert.Null(s.KorunmaOrani);
        Assert.Null(s.BeklenenOy);
        Assert.Null(s.KayipOy);
    }

    [Fact]
    public void HicPusuladaOlunmadiysa_SonSecimYok()
    {
        var r = Hesapla(K(1, 2009, 100, null), K(1, 2024, 300, null));
        Assert.Null(r.SonYil);
        Assert.Null(Assert.Single(r.Satirlar).SonOrani);
    }

    [Fact]
    public void TabandaOlmayanBirim_BosDegerVeNot()
    {
        var r = Hesapla(K(1, 2009, 100, 10), K(1, 2024, 300, 6), K(2, 2024, 200, 4));

        var yeni = r.Satirlar.Single(s => s.BirimKod == 2);
        Assert.Null(yeni.TabanOrani);
        Assert.Null(yeni.KayipOy);
        Assert.Equal(2m, yeni.SonOrani);
        Assert.Contains("2009'da yok", yeni.Not);
        Assert.Equal(1, r.Satirlar[0].BirimKod); // kaybı hesaplanabilenler önce
    }

    [Fact]
    public void Siralama_KayipOyaGore()
    {
        var r = Hesapla(
            K(1, 2009, 100, 10), K(1, 2024, 100, 9),    // kayıp 1
            K(2, 2009, 100, 10), K(2, 2024, 100, 2),    // kayıp 8
            K(3, 2009, 100, 10), K(3, 2024, 100, 5));   // kayıp 5

        Assert.Equal([2, 3, 1], r.Satirlar.Select(s => s.BirimKod));
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 2)]   // 1,2 yukarı yuvarlanır
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    public void Kaleler_YuzdeYirmiYukariYuvarlanir(int birim, int beklenen)
    {
        var d = Enumerable.Range(1, birim).Select(i => K(i, 2009, 100, i)).ToArray();

        var kaleler = ErimeHesabi.Kaleler(d);

        Assert.Equal(beklenen, kaleler.Count);
        Assert.Equal(birim, kaleler[0]); // en yüksek oran
    }

    [Fact]
    public void Kaleler_EsitlikteKucukKod_YalnizcaMeclis2009()
    {
        var d = new[]
        {
            K(30, 2009, 100, 10), K(20, 2009, 100, 10), K(10, 2009, 100, 1), K(40, 2009, 100, 2), K(50, 2009, 100, 3),
            K(10, 2009, 100, 90, OyTuru.BelediyeBaskani),   // başkanlık oranı sayılmaz
            K(10, 2014, 100, 90),                            // 2014 sayılmaz
        };

        Assert.Equal([20], ErimeHesabi.Kaleler(d));
    }

    [Fact]
    public void KaleOrtalamasi_Agirlikli()
    {
        // Kale grubu (ilk %20 = 2 birim / 6): birim 6 ve 5. 2024'te 6: 2/100, 5: 18/300 → ağırlıklı 20/400 = %5
        // (basit ortalama %4 olurdu).
        var d = new List<KarneDegeri>();
        for (int i = 1; i <= 6; i++) d.Add(K(i, 2009, 100, i * 2));
        d.AddRange([K(6, 2024, 100, 2), K(5, 2024, 300, 18), K(1, 2024, 100, 50)]);

        var r = Hesapla([.. d]);

        Assert.Equal([6, 5], r.KaleKodlari);
        var y2024 = r.KaleOrtalamalari.Single(k => k.Yil == 2024);
        Assert.Equal((2, 400L, (long?)20L, (decimal?)5m), (y2024.BirimSayisi, y2024.ToplamOy, y2024.DspOyu, y2024.Oran));
        Assert.Equal(11m, r.KaleOrtalamalari.Single(k => k.Yil == 2009).Oran); // (12 + 10) / 200
        Assert.True(r.Satirlar.Single(s => s.BirimKod == 6).KaleMi);
    }

    [Fact]
    public void KaleOrtalamasi_PusuladaOlmayanYildaBos()
    {
        var r = Hesapla(K(1, 2009, 100, 10), K(1, 2024, 300, null));
        Assert.Null(r.KaleOrtalamalari.Single(k => k.Yil == 2024).Oran);
    }

    [Fact]
    public void BaskanlikSerisi_KaleGrubuMeclistenGelir()
    {
        var d = new[]
        {
            K(1, 2009, 100, 30), K(2, 2009, 100, 1), K(3, 2009, 100, 2), K(4, 2009, 100, 3), K(5, 2009, 100, 4),
            K(1, 2009, 100, 1, OyTuru.BelediyeBaskani), K(2, 2009, 100, 50, OyTuru.BelediyeBaskani),
        };

        var r = ErimeHesabi.Hesapla(d, OyTuru.BelediyeBaskani, ErimeHesabi.Kaleler(d));

        Assert.Equal([1], r.KaleKodlari);
        Assert.Equal(2, r.Satirlar.Count); // yalnızca başkanlık satırları
        Assert.Equal(1m, r.KaleOrtalamalari.Single().Oran);
    }
}
