using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

/// <summary>Erime hesabında birim başına taban yılı (karşılaştırılabilir dönem) ve ilçe düzeyi erime.</summary>
public class ErimeDonemTestleri
{
    private static KarneDegeri K(int kod, int yil, long toplam, long? dsp) =>
        new(kod, $"B{kod}", yil, OyTuru.BelediyeMeclisi, toplam, dsp, dsp is not null);

    [Fact]
    public void TabanYili_BirimBasinaKullanilir()
    {
        KarneDegeri[] d =
        [
            K(1, 2009, 100, 10), K(1, 2019, 100, 4), K(1, 2024, 200, 6),   // 2019'da sınırı değişti
            K(2, 2009, 100, 5), K(2, 2019, 100, 4), K(2, 2024, 200, 6),    // 2009'dan beri sabit
        ];

        var r = ErimeHesabi.Hesapla(d, OyTuru.BelediyeMeclisi, [], new Dictionary<int, int> { [1] = 2019, [2] = 2009 });

        var b1 = r.Satirlar.Single(s => s.BirimKod == 1);
        Assert.Equal((2019, 4m, 3m, 0.75m, 8m, 2m), (b1.TabanYili, b1.TabanOrani!.Value, b1.SonOrani!.Value, b1.KorunmaOrani!.Value, b1.BeklenenOy!.Value, b1.KayipOy!.Value));
        var b2 = r.Satirlar.Single(s => s.BirimKod == 2);
        Assert.Equal((2009, 5m), (b2.TabanYili, b2.TabanOrani!.Value));
        Assert.Equal([2, 1], r.Satirlar.Select(s => s.BirimKod)); // önce en uzun dönem (2009), sonra 2019
    }

    [Fact]
    public void KaleAdaylari_YalnizcaVerilenBirimler()
    {
        KarneDegeri[] d = [K(1, 2009, 100, 50), K(2, 2009, 100, 5), K(3, 2009, 100, 4), K(4, 2009, 100, 3), K(5, 2009, 100, 2)];

        Assert.Equal([1], ErimeHesabi.Kaleler(d));
        Assert.Equal([2], ErimeHesabi.Kaleler(d, new HashSet<int> { 2, 3, 4 })); // 1'in sınırı değişti
    }

    [Fact]
    public void IlceErimesi_2009TabaniylaHerSecim()
    {
        KarneDegeri[] d =
        [
            K(1, 2009, 100, 6), K(2, 2009, 100, 4),     // ilçe: 10 / 200 = %5
            K(1, 2024, 300, 3), K(2, 2024, 200, 2),     // ilçe: 5 / 500 = %1
        ];

        var satirlar = ErimeHesabi.IlceErimesi(d, OyTuru.BelediyeMeclisi);

        Assert.Equal([2009, 2024], satirlar.Select(s => s.Yil));
        var son = satirlar[1];
        Assert.Equal((500L, 5L, 1m, -4m, 0.2m, 25m, 20m),
            (son.ToplamOy, son.DspOyu!.Value, son.Oran!.Value, son.PuanFarki!.Value, son.KorunmaOrani!.Value, son.BeklenenOy!.Value, son.KayipOy!.Value));
        Assert.Equal(1m, satirlar[0].KorunmaOrani);
    }

    [Fact]
    public void IlceErimesi_PusuladaOlmayanSecimdeBos()
    {
        var satirlar = ErimeHesabi.IlceErimesi([K(1, 2009, 100, 5), K(1, 2024, 300, null)], OyTuru.BelediyeMeclisi);

        var son = satirlar.Single(s => s.Yil == 2024);
        Assert.Null(son.Oran);
        Assert.Null(son.DspOyu);
        Assert.Null(son.KayipOy);
        Assert.Equal(300, son.ToplamOy);
    }
}
