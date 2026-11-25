using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Sayfalardaki raporların Excel/PDF belge modeline çevrilmesi.</summary>
public class RaporBelgeleriTestleri
{
    internal static readonly IlceSecenegi Ilce = new(1, "34-ESENYURT", "ESENYURT", "İSTANBUL");
    internal static readonly SecimSecenegi Secim2024 = new(4, 2024, new DateOnly(2024, 3, 31), "2024 Yerel Seçimleri", SecimTuru.Yerel);
    internal static readonly DateTime Uretim = new(2026, 11, 25, 14, 30, 0);

    private static BirimYilDurumu D(int yil, SinirDurumu s, decimal? degisim = null) => new(yil, s, s, null, degisim, null);

    internal static KarneSonucu OrnekKarne()
    {
        KarneSatiri[] satirlar =
        [
            new(119318, "AKÇABURGAZ MAH.", 48, 12274, true, 39, 0.3177m, D(2024, SinirDurumu.Sabit), 2009, [D(2019, SinirDurumu.Supheli, 0.48m)]),
            new(147455, "SELAHADDİN EYYUBİ MAH.", 54, 12804, true, 56, 0.4374m, D(2024, SinirDurumu.Sabit), 2019, []),
        ];
        var karne = new Karne(Ilce, Secim2024, OyTuru.BelediyeMeclisi, SinirEsikleri.Varsayilan, satirlar);
        var sorgu = new KarneSorgusu("34-ESENYURT", 4, "belediye-meclisi", "dsp-orani", "azalan");
        return new KarneSonucu([Ilce], [Secim2024], [OyTuru.BelediyeMeclisi], karne, KarneSiraAlani.DspOrani, true, sorgu);
    }

    [Fact]
    public void Karne_BaslikBilgisi()
    {
        var b = RaporBelgeleri.Karne(OrnekKarne(), ["2024_yerel_belediye-meclisi_esenyurt.xlsx (yüklendi 3 Kasım 2026)"], Uretim);

        Assert.Equal("Mahalle karnesi", b.Baslik);
        Assert.Equal("İSTANBUL / ESENYURT · 2024 Yerel Seçimleri (31 Mart 2024) · Belediye meclisi · sıra: DSP oranı azalan", b.FiltreOzeti);
        Assert.Equal("25 Kasım 2026 14:30 (Türkiye saati)", b.UretimMetni);
        Assert.Equal("karne_34-ESENYURT_2024_belediye-meclisi", b.DosyaAdi);
        Assert.Single(b.Kaynaklar);
        Assert.Contains(b.Notlar, n => n.StartsWith("Veri kaynağı: YSK", StringComparison.Ordinal));
        Assert.Contains(b.Notlar, n => n.Contains("düşüş %25", StringComparison.Ordinal));
        Assert.Contains(Metinler.EslemeNotu, b.Notlar);
    }

    [Fact]
    public void Karne_SatirlarEkrandakiSirada_UyariSutunuVeToplam()
    {
        var t = RaporBelgeleri.Karne(OrnekKarne(), [], Uretim).Bolumler.Single();

        Assert.Equal(10, t.Sutunlar.Count);
        Assert.Equal(3, t.Satirlar.Count); // 2 birim + toplam
        var a = t.Satirlar[0];
        Assert.Equal(SatirTuru.Uyari, a.Tur);
        Assert.Equal([1, 119318, "AKÇABURGAZ MAH.", 48, 12274L, (long?)39, (decimal?)0.3177m, "2009–2024", "Sabit"], a.Degerler.Take(9));
        Assert.Contains("2019 seçiminde ilçe meclis oyundaki pay +%48 değişti", (string)a.Degerler[9]!);
        Assert.Equal(SatirTuru.Normal, t.Satirlar[1].Tur);
        Assert.Null(t.Satirlar[1].Degerler[9]);

        var toplam = t.Satirlar[2];
        Assert.Equal(SatirTuru.Toplam, toplam.Tur);
        Assert.Equal((102, 25078L, (long?)95), ((int)toplam.Degerler[3]!, (long)toplam.Degerler[4]!, (long?)toplam.Degerler[5]));
        Assert.Equal(100m * 95 / 25078, toplam.Degerler[6]);
    }

    [Fact]
    public void Karne_PusuladaDegilse_DspBosVeToplamBos()
    {
        var s = OrnekKarne();
        var bos = s.Karne! with
        {
            OyTuru = OyTuru.BelediyeBaskani,
            Satirlar = [.. s.Karne!.Satirlar.Select(x => x with { PusuladaMi = false, DspOyu = null, DspOrani = null })],
        };

        var t = RaporBelgeleri.Karne(s with { Karne = bos }, [], Uretim).Bolumler.Single();

        Assert.All(t.Satirlar, r => Assert.Null(r.Degerler[5]));
        Assert.All(t.Satirlar, r => Assert.Null(r.Degerler[6]));
    }

    [Fact]
    public void Mahalle_SinirDegistigiSecimdePuanFarkiUyarisi()
    {
        BirimYilDurumu[] yillar = [D(2014, SinirDurumu.Sabit), D(2019, SinirDurumu.SiniriDegisti, -0.83m)];
        var d = new MahalleDetayi(Ilce, 119325, "ATATÜRK MAH.", new BirimDonemi(119325, "ATATÜRK MAH.", 2019, 2019, yillar),
            SinirEsikleri.Varsayilan,
            [
                new(4, 2019, OyTuru.BelediyeMeclisi, 25, 6802, true, 22, 0.32m, 2014, 0.24m, yillar[1]),
                new(3, 2014, OyTuru.BelediyeMeclisi, 124, 31758, true, 27, 0.09m, null, null, yillar[0]),
            ]);

        var b = RaporBelgeleri.Mahalle(d, [], Uretim);

        Assert.Equal(["Belediye meclisi", "Sınır geçmişi"], b.Bolumler.Select(x => x.SayfaAdi));
        var meclis = b.Bolumler[0];
        Assert.Equal([2014, 2019], meclis.Satirlar.Select(r => (int)r.Degerler[0]!)); // eskiden yeniye
        Assert.Null(meclis.Satirlar[0].Degerler[7]);
        Assert.Equal("Sınır 2019 seçiminde değişti; 2014 ile karşılaştırma aynı toprağı göstermez.", meclis.Satirlar[1].Degerler[7]);
        Assert.Equal(-83m, b.Bolumler[1].Satirlar[1].Degerler[2]); // pay değişimi yüzde
        Assert.Equal("mahalle_34-ESENYURT_119325", b.DosyaAdi);
    }

    [Fact]
    public void Erime_DonemGruplariVeDonemSutunu()
    {
        BirimYilDurumu[] sabit = [D(2009, SinirDurumu.Sabit), D(2024, SinirDurumu.Sabit)];
        BirimYilDurumu[] yeni = [D(2009, SinirDurumu.SonradanKuruldu), D(2019, SinirDurumu.Yeni), D(2024, SinirDurumu.Sabit)];
        var sinir = new SinirAnaliziSonucu(SinirEsikleri.Varsayilan, [2009, 2019, 2024],
            [new BirimDonemi(1, "A", 2009, 2024, sabit), new BirimDonemi(2, "B", 2019, 2024, yeni)]);
        ErimeSatiri S(int kod, string ad, int taban, decimal kayip) =>
            new(kod, ad, taban, 1m, 100, 1, 0.5m, 200, 1, -0.5m, 0.5m, 2m, kayip, false, null);
        var rapor = new ErimeRaporu(OyTuru.BelediyeMeclisi, 2009, 2024, [S(1, "A", 2009, 1m), S(2, "B", 2019, 5m)], [1],
            [new KaleOrtalamasi(2009, 1, 100, 1, 1m), new KaleOrtalamasi(2024, 1, 200, 1, 0.5m)]);
        var e = new ErimeSonucu([Ilce], Ilce, [OyTuru.BelediyeMeclisi], OyTuru.BelediyeMeclisi,
            new Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>> { [OyTuru.BelediyeMeclisi] = [new(2009, 1000, 10, 1m, 0m, 1m, 10m, 0m)] },
            new Dictionary<OyTuru, ErimeRaporu> { [OyTuru.BelediyeMeclisi] = rapor }, sinir, new ErimeSorgusu("34-ESENYURT", "belediye-meclisi"));

        var b = RaporBelgeleri.Erime(e, [], Uretim);

        Assert.Equal(["İlçe düzeyi", "Mahalle düzeyi", "Kaleler"], b.Bolumler.Select(x => x.SayfaAdi));
        var m = b.Bolumler[1].Satirlar;
        Assert.Equal([SatirTuru.Grup, SatirTuru.Normal, SatirTuru.Grup, SatirTuru.Normal], m.Select(r => r.Tur));
        Assert.Equal("Dönem 2009–2024 · 1 birim", m[0].Degerler[0]);
        Assert.Equal((1, "2009–2024"), ((int)m[1].Degerler[0]!, (string)m[1].Degerler[3]!));
        Assert.Equal((2, "2019–2024"), ((int)m[3].Degerler[0]!, (string)m[3].Degerler[3]!)); // sıra numarası gruplar boyunca sürer
        Assert.Equal(["Seri", "2009", "2024"], b.Bolumler[2].Sutunlar.Select(s => s.Baslik));
        Assert.Equal("erime_34-ESENYURT_belediye-meclisi", b.DosyaAdi);
    }
}
