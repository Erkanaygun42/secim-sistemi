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

    internal static readonly KaynakListesi Kaynaklar = new(
        ["2009_yerel_belediye-meclisi_esenyurt.xlsx (yüklendi 3 Kasım 2026)", "2024_yerel_belediye-meclisi_esenyurt.xlsx (yüklendi 3 Kasım 2026)"],
        new DateTime(2026, 11, 3, 10, 0, 0));

    private static BirimYilDurumu D(int yil, SinirDurumu s, decimal? degisim = null) => new(yil, s, s, null, degisim, null);

    internal static KarneSonucu OrnekKarne()
    {
        KarneSatiri[] satirlar =
        [
            new(119318, "AKÇABURGAZ MAH.", 48, 12274, true, 39, 0.3177m, D(2024, SinirDurumu.Sabit),
                new DonemSiniri(SinirDurumu.Supheli, 2009, 2024, [D(2019, SinirDurumu.Supheli, 0.48m)]), 2019, -0.16m),
            new(147455, "SELAHADDİN EYYUBİ MAH.", 54, 12804, true, 56, 0.4374m, D(2024, SinirDurumu.Sabit),
                new DonemSiniri(SinirDurumu.Yeni, 2019, 2024, [D(2019, SinirDurumu.Yeni)]), 2019, 0.1m),
        ];
        var karne = new Karne(Ilce, Secim2024, OyTuru.BelediyeMeclisi, SinirEsikleri.Varsayilan, satirlar);
        var sorgu = new KarneSorgusu("34-ESENYURT", 4, "belediye-meclisi", "dsp-orani", "azalan");
        return new KarneSonucu([Ilce], [Secim2024], [OyTuru.BelediyeMeclisi], karne, KarneSiraAlani.DspOrani, true, sorgu);
    }

    [Fact]
    public void Karne_BaslikBilgisi()
    {
        var b = RaporBelgeleri.Karne(OrnekKarne(), Kaynaklar, Uretim);

        Assert.Equal("Mahalle karnesi", b.Baslik);
        Assert.Equal("İSTANBUL / ESENYURT · 2024 Yerel Seçimleri (31 Mart 2024) · Belediye meclisi · sıra: DSP oranı azalan", b.FiltreOzeti);
        Assert.Equal("25 Kasım 2026 14:30 (Türkiye saati)", b.UretimMetni);
        Assert.Equal("karne_34-ESENYURT_2024_belediye-meclisi", b.DosyaAdi);
        Assert.Equal("YSK Açık Veri Portalı, sandık düzeyi sonuçlar · 2 dosya · son yükleme 3 Kasım 2026", b.Kaynaklar.Ozet);
        Assert.Contains(b.Notlar, n => n.StartsWith("Veri kaynağı: YSK", StringComparison.Ordinal));
        Assert.Contains(b.Notlar, n => n.Contains("düşüş %25", StringComparison.Ordinal));
        Assert.Contains(Metinler.EslemeNotu, b.Notlar);
    }

    [Fact]
    public void Karne_SatirlarEkrandakiSirada_PuanFarkiDonemSiniriVeUyari()
    {
        var t = RaporBelgeleri.Karne(OrnekKarne(), KaynakListesi.Bos, Uretim).Bolumler.Single();

        Assert.Equal(["#", "Kod", "Mahalle/köy", "Sandık", "Toplam oy", "DSP oyu", "DSP %", "Puan farkı (2019'a göre)",
            "Karşılaştırılabilir dönem", "Sınır durumu", "Uyarı"], t.Sutunlar.Select(s => s.Baslik));
        Assert.Equal(3, t.Satirlar.Count); // 2 birim + toplam
        var a = t.Satirlar[0];
        Assert.Equal(SatirTuru.Uyari, a.Tur);
        Assert.Equal([1, 119318, "AKÇABURGAZ MAH.", 48, 12274L, (long?)39, (decimal?)0.3177m, (decimal?)-0.16m, "2009–2024", "Şüpheli (2019)"],
            a.Degerler.Take(10));
        Assert.Contains("2019 seçiminde ilçe meclis oyundaki pay +%48 değişti", (string)a.Degerler[10]!);
        var y = t.Satirlar[1];
        Assert.Equal((SatirTuru.Normal, "2019–2024", "Yeni (2019)"), (y.Tur, (string)y.Degerler[8]!, (string)y.Degerler[9]!));
        Assert.Null(y.Degerler[10]); // puan farkı 2019'dan 2024'e: sınır 2024'te değişmedi, uyarı yok

        var toplam = t.Satirlar[2];
        Assert.Equal(SatirTuru.Toplam, toplam.Tur);
        Assert.Equal((102, 25078L, (long?)95), ((int)toplam.Degerler[3]!, (long)toplam.Degerler[4]!, (long?)toplam.Degerler[5]));
        Assert.Equal(100m * 95 / 25078, toplam.Degerler[6]);
    }

    [Fact]
    public void Karne_SinirTamBuSecimdeDegistiyse_PuanFarkiUyarisi()
    {
        var s = OrnekKarne();
        var satir = s.Karne!.Satirlar[1] with
        {
            DonemSiniri = new DonemSiniri(SinirDurumu.SiniriDegisti, 2024, 2024, [D(2024, SinirDurumu.SiniriDegisti, -0.4m)]),
        };

        var t = RaporBelgeleri.Karne(s with { Karne = s.Karne with { Satirlar = [satir] } }, KaynakListesi.Bos, Uretim).Bolumler.Single();

        Assert.True(satir.PuanKesintili);
        Assert.Equal("Sınırı değişti (2024)", t.Satirlar[0].Degerler[9]);
        Assert.Equal("Sınır 2024 seçiminde değişti; 2019 ile puan farkı aynı toprağı göstermez.", t.Satirlar[0].Degerler[10]);
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

        var t = RaporBelgeleri.Karne(s with { Karne = bos }, KaynakListesi.Bos, Uretim).Bolumler.Single();

        Assert.All(t.Satirlar, r => Assert.Null(r.Degerler[5]));
        Assert.All(t.Satirlar, r => Assert.Null(r.Degerler[6]));
    }

    [Fact]
    public void Karne_NasilOkunur_SozluktenHerSutunBirKez()
    {
        var b = RaporBelgeleri.Karne(OrnekKarne(), KaynakListesi.Bos, Uretim);

        var adlar = b.Okuma.Select(t => t.Ad).ToList();
        Assert.Equal(adlar.Distinct().Count(), adlar.Count);
        Assert.Contains("Karşılaştırılabilir dönem", adlar);
        Assert.Contains("Sınır durumu", adlar);
        Assert.Contains("Puan farkı", adlar);
        Assert.DoesNotContain("#", adlar);
        Assert.All(b.Okuma, t => Assert.False(string.IsNullOrWhiteSpace(t.Aciklama)));
        Assert.Equal("Mahalle sınırlarının aynı kaldığı dönem.", SutunSozlugu.Donem.Aciklama);
        Assert.Equal("2019–2024: mahalle 2019'da kuruldu ya da sınırı değişti, daha eski yıllarla karşılaştırılamaz.", SutunSozlugu.Donem.Ornek);
    }

    [Fact]
    public void Mahalle_OzetGrafigi_SinirDegistigiSecimdePuanFarkiUyarisi()
    {
        BirimYilDurumu[] yillar = [D(2014, SinirDurumu.Sabit), D(2019, SinirDurumu.SiniriDegisti, -0.83m)];
        var birim = new BirimDonemi(119325, "ATATÜRK MAH.", 2019, 2019, yillar);
        var d = new MahalleDetayi(Ilce, 119325, "ATATÜRK MAH.", birim, SinirEsikleri.Varsayilan,
            [
                new(4, 2019, OyTuru.BelediyeMeclisi, 25, 6802, true, 22, 0.32m, 2014, 0.24m, yillar[1], birim.DonemSiniri(2019)),
                new(3, 2014, OyTuru.BelediyeMeclisi, 124, 31758, true, 27, 0.09m, null, null, yillar[0], birim.DonemSiniri(2014)),
            ]);

        var b = RaporBelgeleri.Mahalle(d, KaynakListesi.Bos, Uretim);

        Assert.Equal(["Özet", "Belediye meclisi", "Sınır geçmişi"], b.Bolumler.Select(x => x.SayfaAdi));
        var ozet = b.Bolumler[0];
        var grafik = Assert.IsType<CizgiGrafigi>(Assert.Single(ozet.GrafikListesi));
        Assert.Equal(2019, grafik.DonemBaslangici);
        Assert.Equal([0.09m, 0.32m], grafik.Seriler.Single().Degerler);
        Assert.Equal([2014, 0.09m, "dönem dışı"], ozet.Satirlar[0].Degerler);

        var meclis = b.Bolumler[1];
        Assert.Equal([2014, 2019], meclis.Satirlar.Select(r => (int)r.Degerler[0]!)); // eskiden yeniye
        Assert.Null(meclis.Satirlar[0].Degerler[7]);
        Assert.Equal("Sınırı değişti (2019)", meclis.Satirlar[1].Degerler[6]);
        Assert.Equal("Sınır 2019 seçiminde değişti; 2014 ile puan farkı aynı toprağı göstermez.", meclis.Satirlar[1].Degerler[7]);
        Assert.Equal(-83m, b.Bolumler[2].Satirlar[1].Degerler[2]); // pay değişimi yüzde
        Assert.Equal("mahalle_34-ESENYURT_119325", b.DosyaAdi);
    }

    internal static ErimeSonucu OrnekErime()
    {
        BirimYilDurumu[] sabit = [D(2009, SinirDurumu.Sabit), D(2019, SinirDurumu.Sabit), D(2024, SinirDurumu.Sabit)];
        BirimYilDurumu[] yeni = [D(2009, SinirDurumu.SonradanKuruldu), D(2019, SinirDurumu.Yeni), D(2024, SinirDurumu.Sabit)];
        var sinir = new SinirAnaliziSonucu(SinirEsikleri.Varsayilan, [2009, 2019, 2024],
            [new BirimDonemi(1, "A", 2009, 2024, sabit), new BirimDonemi(2, "B", 2019, 2024, yeni)]);
        ErimeSatiri S(int kod, string ad, int taban, decimal kayip, Dictionary<int, decimal?> oranlar) =>
            new(kod, ad, taban, 1m, 100, 1, 0.5m, 200, 1, -0.5m, 0.5m, 2m, kayip, kod == 1, null, oranlar);
        var rapor = new ErimeRaporu(OyTuru.BelediyeMeclisi, 2009, 2024,
            [
                S(1, "A", 2009, 1m, new() { [2009] = 1m, [2019] = 0.7m, [2024] = 0.5m }),
                S(2, "B", 2019, 5m, new() { [2009] = 3m, [2019] = 1m, [2024] = 0.5m }),
            ], [1],
            [new KaleOrtalamasi(2009, 1, 100, 1, 1m), new KaleOrtalamasi(2024, 1, 200, 1, 0.5m)], [2009, 2019, 2024]);
        return new ErimeSonucu([Ilce], Ilce, [OyTuru.BelediyeMeclisi, OyTuru.BelediyeBaskani], OyTuru.BelediyeMeclisi,
            new Dictionary<OyTuru, IReadOnlyList<IlceErimeSatiri>>
            {
                [OyTuru.BelediyeBaskani] = [new(2009, 1000, null, null, null, null, null, null), new(2024, 2000, 4, 0.2m, null, null, null, null)],
                [OyTuru.BelediyeMeclisi] = [new(2009, 1000, 10, 1m, 0m, 1m, 10m, 0m), new(2024, 2000, 6, 0.3m, -0.7m, 0.3m, 20m, 14m)],
            },
            new Dictionary<OyTuru, ErimeRaporu> { [OyTuru.BelediyeMeclisi] = rapor }, sinir, new ErimeSorgusu("34-ESENYURT", "belediye-meclisi"));
    }

    [Fact]
    public void Erime_DonemGruplari_YillarYanYana_DonemDisiGri()
    {
        var b = RaporBelgeleri.Erime(OrnekErime(), KaynakListesi.Bos, Uretim);

        Assert.Equal(["İlçe düzeyi", "Mahalle düzeyi", "2009 kaleleri"], b.Bolumler.Select(x => x.SayfaAdi));
        var bolum = b.Bolumler[1];
        Assert.Equal(["#", "Kod", "Mahalle/köy", "Karşılaştırılabilir dönem", "2009 %", "2019 %", "2024 %"], bolum.Sutunlar.Take(7).Select(s => s.Baslik));
        Assert.Contains(bolum.Sutunlar, s => s.Baslik == "2009 kalesi");
        Assert.DoesNotContain(bolum.Sutunlar, s => s.Baslik is "Taban %" or "Son %" or "Kale");

        var m = bolum.Satirlar;
        Assert.Equal([SatirTuru.Grup, SatirTuru.Normal, SatirTuru.Grup, SatirTuru.Normal], m.Select(r => r.Tur));
        Assert.Equal("Dönem 2009–2024 · 1 birim", m[0].Degerler[0]);
        Assert.Equal((1, "2009–2024"), ((int)m[1].Degerler[0]!, (string)m[1].Degerler[3]!));
        Assert.Equal([1m, 0.7m, 0.5m], m[1].Degerler.Skip(4).Take(3));
        Assert.Null(m[1].DonemDisi);
        Assert.Equal("evet", m[1].Degerler[^2]);

        Assert.Equal((2, "2019–2024"), ((int)m[3].Degerler[0]!, (string)m[3].Degerler[3]!)); // sıra numarası gruplar boyunca sürer
        Assert.Equal([4], m[3].DonemDisi!);                  // 2009, B'nin dönemi dışında
        Assert.Null(m[3].Degerler[4]);                        // ... ve değeri yazılmaz
        Assert.Equal([1m, 0.5m], m[3].Degerler.Skip(5).Take(2));

        Assert.Equal(["Seri", "2009 %", "2024 %"], b.Bolumler[2].Sutunlar.Select(s => s.Baslik));
        Assert.Equal("erime_34-ESENYURT_belediye-meclisi", b.DosyaAdi);
    }

    [Fact]
    public void Erime_Grafikler_IlceCizgisiVeEnCokKaybedenler()
    {
        var e = OrnekErime();
        var b = RaporBelgeleri.Erime(e, KaynakListesi.Bos, Uretim);

        var cizgi = Assert.IsType<CizgiGrafigi>(Assert.Single(b.Bolumler[0].GrafikListesi));
        Assert.Equal([2009, 2024], cizgi.Yillar);
        // Renk seriyi izler: meclis 0, başkan 1; meclis önce.
        Assert.Equal([("Belediye meclisi", 0), ("Belediye başkanı", 1)], cizgi.Seriler.Select(s => (s.Ad, s.Renk)));
        Assert.Equal([null, 0.2m], cizgi.Seriler[1].Degerler); // 2009'da pusulada değil: boşluk

        var cubuk = Assert.IsType<CubukGrafigi>(Assert.Single(b.Bolumler[1].GrafikListesi));
        Assert.Equal(["B (2019–2024)", "A (2009–2024)"], cubuk.Cubuklar.Select(c => c.Etiket)); // kayıp büyükten küçüğe
        Assert.Equal([5m, 1m], cubuk.Cubuklar.Select(c => c.Deger));
    }

    [Fact]
    public void EnCokKaybeden_KaybiOlmayanlarGirmez_EnFazlaOn()
    {
        var e = OrnekErime();
        var r = e.Rapor!;
        var satirlar = Enumerable.Range(1, 14).Select(i => r.Satirlar[0] with { BirimKod = 100 + i, BirimAdi = $"M{i}", KayipOy = i - 3 }).ToList();
        var e2 = e with { Raporlar = new Dictionary<OyTuru, ErimeRaporu> { [OyTuru.BelediyeMeclisi] = r with { Satirlar = satirlar } } };

        var g = RaporGrafikleri.EnCokKaybeden(e2)!;

        Assert.Equal(10, g.Cubuklar.Count);
        Assert.Equal(11m, g.Cubuklar[0].Deger);
        Assert.All(g.Cubuklar, c => Assert.True(c.Deger > 0));
    }

    [Theory]
    [InlineData(2009, "2009'a")]
    [InlineData(2014, "2014'e")]
    [InlineData(2019, "2019'a")]
    [InlineData(2024, "2024'e")]
    [InlineData(2002, "2002'ye")]
    [InlineData(2006, "2006'ya")]
    [InlineData(2010, "2010'a")]
    [InlineData(2020, "2020'ye")]
    [InlineData(2000, "2000'e")]
    public void Yonelme_OkunusaGoreEk(int yil, string beklenen) => Assert.Equal(beklenen, Metinler.Yonelme(yil));
}
