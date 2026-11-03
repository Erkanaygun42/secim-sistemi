using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

public class SinirAnaliziTestleri
{
    private static readonly SinirEsikleri E = SinirEsikleri.Varsayilan;

    private static SinirBirimi B(int kod, params (int Yil, long Oy)[] oylar) =>
        new(kod, $"B{kod}", oylar.ToDictionary(x => x.Yil, x => x.Oy), new HashSet<int>());

    private static SinirAnaliziSonucu Hesapla(int[] yillar, SinirBirimi[] birimler, int[]? yeniYillar = null,
        SinirKarari[]? kararlar = null, SinirEsikleri? esikler = null) =>
        SinirAnalizi.Hesapla(new SinirGirdisi(yillar, birimler, (yeniYillar ?? []).ToHashSet(), kararlar ?? []), esikler ?? E);

    private static SinirDurumu Durum(SinirAnaliziSonucu s, int kod, int yil) => s.Birim(kod)!.Yillar.Single(y => y.Yil == yil).Sonuc;

    [Fact]
    public void PayIlceyleUyumlu_Sabit()
    {
        // A ve B ilçeyle birlikte büyüdü, payları değişmedi; ilçede yeni mahalle de var.
        var s = Hesapla([2014, 2019], [B(1, (2014, 100), (2019, 200)), B(2, (2014, 300), (2019, 600)), B(3, (2019, 0))], yeniYillar: [2019]);

        Assert.Equal(SinirDurumu.Sabit, Durum(s, 1, 2019));
        Assert.Equal(2014, s.Birim(1)!.DonemBaslangici);
    }

    [Fact]
    public void SonradanKurulan_OncekiSecimdeBosDegerli()
    {
        var s = Hesapla([2014, 2019], [B(1, (2014, 100), (2019, 100)), B(2, (2019, 50))], yeniYillar: [2019]);

        var y2014 = s.Birim(2)!.Yillar.Single(y => y.Yil == 2014);
        Assert.Equal(SinirDurumu.SonradanKuruldu, y2014.Sonuc);
        Assert.Null(y2014.Pay);
        Assert.Null(y2014.PayDegisimi);
        Assert.Equal(SinirDurumu.Yeni, Durum(s, 2, 2019));
        Assert.Equal(2019, s.Birim(2)!.DonemBaslangici);
    }

    [Fact]
    public void BuyukDusus_VeYeniMahalle_SiniriDegisti_DonemKesilir()
    {
        // A'nın payı %50 → %20 (−%60); aynı seçimde B kuruldu.
        var s = Hesapla([2009, 2014, 2019], [B(1, (2009, 50), (2014, 50), (2019, 40)), B(2, (2009, 50), (2014, 50), (2019, 60)), B(3, (2019, 100))], yeniYillar: [2019]);

        Assert.Equal(SinirDurumu.SiniriDegisti, Durum(s, 1, 2019));
        Assert.Equal(2019, s.Birim(1)!.DonemBaslangici);
        Assert.Equal("2019", s.Birim(1)!.Donem);
    }

    [Fact]
    public void BuyukDusus_AmaYeniMahalleYok_Sabit()
    {
        // Pay yarıya indi ama ilçede yeni mahalle yok: göç ya da büyüme farkı, sınır değişikliği değil.
        var s = Hesapla([2014, 2019], [B(1, (2014, 100), (2019, 50)), B(2, (2014, 100), (2019, 150))]);

        Assert.Equal(SinirDurumu.Sabit, Durum(s, 1, 2019));
        Assert.Equal(SinirDurumu.Sabit, s.Birim(1)!.Yillar.Single(y => y.Yil == 2019).Otomatik);
    }

    [Theory]
    [InlineData(100, 74, SinirDurumu.SiniriDegisti)] // −%26
    [InlineData(100, 76, SinirDurumu.Supheli)]       // −%24
    [InlineData(100, 84, SinirDurumu.Supheli)]       // −%16
    [InlineData(100, 86, SinirDurumu.Sabit)]         // −%14
    [InlineData(100, 140, SinirDurumu.Supheli)]      // +%40
    [InlineData(100, 161, SinirDurumu.SiniriDegisti)] // +%61
    public void Esikler_DususArtisSupheli(long once, long sonra, SinirDurumu beklenen)
    {
        // Ölçülen birim payını doğrudan belirlemek için ilçenin geri kalanı 2019'da 2014'e göre aynı oyla durur.
        var s = Hesapla([2014, 2019], [B(1, (2014, once), (2019, sonra)), B(2, (2014, 1_000_000), (2019, 1_000_000)), B(3, (2019, 0))], yeniYillar: [2019]);

        Assert.Equal(beklenen, Durum(s, 1, 2019));
    }

    [Fact]
    public void Supheli_DonemiKesmez()
    {
        var s = Hesapla([2009, 2014, 2019], [B(1, (2009, 100), (2014, 100), (2019, 80)), B(2, (2009, 1_000_000), (2014, 1_000_000), (2019, 1_000_000)), B(3, (2019, 0))], yeniYillar: [2019]);

        Assert.Equal(SinirDurumu.Supheli, Durum(s, 1, 2019));
        Assert.Equal(2009, s.Birim(1)!.DonemBaslangici);
        Assert.True(s.Birim(1)!.SupheliVar);
    }

    [Fact]
    public void EsikAyarlanabilir()
    {
        var birimler = new[] { B(1, (2014, 100), (2019, 80)), B(2, (2014, 1_000_000), (2019, 1_000_000)), B(3, (2019, 0)) };

        Assert.Equal(SinirDurumu.Supheli, Durum(Hesapla([2014, 2019], birimler, [2019]), 1, 2019));
        Assert.Equal(SinirDurumu.SiniriDegisti, Durum(Hesapla([2014, 2019], birimler, [2019], esikler: new SinirEsikleri(0.15m, 0.60m, 0.10m)), 1, 2019));
    }

    [Fact]
    public void GecersizEsik_HataVerir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SinirEsikleri(0.25m, 0.60m, 0.30m).Dogrula()); // şüpheli ≥ düşüş
        Assert.Throws<ArgumentOutOfRangeException>(() => new SinirEsikleri(1.2m, 0.60m, 0.10m).Dogrula());
    }

    [Fact]
    public void InsanKarari_OtomatikTespitiGecersizKilar()
    {
        var birimler = new[] { B(1, (2009, 100), (2014, 100), (2019, 80)), B(2, (2009, 1_000_000), (2014, 1_000_000), (2019, 1_000_000)), B(3, (2019, 0)) };

        var sabit = Hesapla([2009, 2014, 2019], birimler, [2019], [new SinirKarari(1, 2019, SinirDurumu.Sabit, "karar #1")]);
        var y = sabit.Birim(1)!.Yillar.Single(x => x.Yil == 2019);
        Assert.Equal((SinirDurumu.Supheli, SinirDurumu.Sabit, true), (y.Otomatik, y.Sonuc, y.Dogrulandi));
        Assert.False(sabit.Birim(1)!.SupheliVar); // raporda "Şüpheli" değil "Doğrulandı" görünür

        var degisti = Hesapla([2009, 2014, 2019], birimler, [2019], [new SinirKarari(1, 2019, SinirDurumu.SiniriDegisti, "karar #2")]);
        Assert.Equal(2019, degisti.Birim(1)!.DonemBaslangici);
    }

    [Fact]
    public void OnayliEsleme_DonemiGeriyeUzatir()
    {
        // Birim = A + B (onaylı eşleme): B 2019'da kuruldu. Birleşik pay tutarlı olmasa bile birim sabit sayılır.
        var esleme = new SinirBirimi(1, "A + B", new Dictionary<int, long> { [2009] = 100, [2014] = 100, [2019] = 40 }, new HashSet<int> { 2019 });
        var s = Hesapla([2009, 2014, 2019], [esleme, B(2, (2009, 100), (2014, 100), (2019, 100)), B(3, (2019, 100))], yeniYillar: [2019]);

        var y = s.Birim(1)!.Yillar.Single(x => x.Yil == 2019);
        Assert.Equal((SinirDurumu.SiniriDegisti, SinirDurumu.Sabit, true), (y.Otomatik, y.Sonuc, y.Dogrulandi));
        Assert.Equal(2009, s.Birim(1)!.DonemBaslangici);
    }

    [Fact]
    public void Referans_SonSecimdekiBirimler()
    {
        var s = Hesapla([2014, 2019], [B(1, (2014, 100), (2019, 100)), B(2, (2014, 100))]);

        Assert.Null(s.Birim(2));
        Assert.Single(s.Birimler);
    }

    /// <summary>
    /// Esenyurt kalibrasyonu (2014 → 2019, belediye meclisi toplam oyu, YSK açık verisinden). 2019'da 23 yeni mahalle
    /// kuruldu; toplamları (220.842) tek kalemde. Küçüldüğü bilinen 14 mahalle "sınırı değişti", diğer 6 mahalle
    /// "sınırı değişti" çıkmamalı. Seçilen eşikler: düşüş %25, artış %60, şüpheli %15 (docs/kararlar.md).
    /// </summary>
    [Fact]
    public void Esenyurt_Kalibrasyonu()
    {
        (int Kod, string Ad, long Oy2014, long Oy2019, bool Kuculdu)[] mahalleler =
        [
            (119318, "AKÇABURGAZ", 5_394, 10_235, false),
            (119319, "ARDIÇLI", 16_398, 3_756, true),
            (119325, "ATATÜRK", 31_758, 6_802, true),
            (119320, "CUMHURİYET", 11_767, 7_389, true),
            (119321, "ESENKENT", 15_468, 5_803, true),
            (119322, "FATİH", 20_305, 12_683, true),
            (119323, "GÜZELYURT", 14_219, 7_448, true),
            (119336, "İNCİRTEPE", 16_450, 13_322, true),
            (119337, "İNÖNÜ", 19_483, 12_747, true),
            (119324, "İSTİKLAL", 34_192, 9_098, true),
            (119326, "MEHTERÇEŞME", 22_848, 17_835, true),
            (119327, "MERKEZ", 11_758, 14_039, false),
            (119328, "NAMIK KEMAL", 12_057, 12_151, false),
            (119331, "ORHAN GAZİ", 4_735, 5_969, false),
            (119335, "ÖRNEK", 26_843, 10_316, true),
            (119329, "PINAR", 13_224, 17_578, false),
            (119330, "SAADETDERE", 22_394, 12_509, true),
            (119332, "TALATPAŞA", 13_528, 12_567, true),
            (119333, "YENİKENT", 20_935, 11_205, true),
            (119334, "YEŞİLKENT", 8_030, 12_575, false),
        ];
        var birimler = mahalleler.Select(m => B(m.Kod, (2014, m.Oy2014), (2019, m.Oy2019))).Append(B(1, (2019, 220_842))).ToArray();
        Assert.Equal(341_786, mahalleler.Sum(m => m.Oy2014));        // ilçe 2014 toplamı
        Assert.Equal(436_869, birimler.Sum(b => b.MeclisOyu.GetValueOrDefault(2019))); // ilçe 2019 toplamı

        var s = Hesapla([2014, 2019], birimler, yeniYillar: [2019]);

        foreach (var m in mahalleler)
        {
            var d = Durum(s, m.Kod, 2019);
            if (m.Kuculdu) Assert.True(d == SinirDurumu.SiniriDegisti, $"{m.Ad}: {d}");
            else Assert.True(d != SinirDurumu.SiniriDegisti, $"{m.Ad}: {d}");
        }
        Assert.Equal(14, mahalleler.Count(m => Durum(s, m.Kod, 2019) == SinirDurumu.SiniriDegisti));
        // Şüpheli: Namık Kemal −%21, Yeşilkent +%23, Akçaburgaz +%49. Sabit: Merkez, Orhan Gazi, Pınar.
        Assert.Equal([119318, 119328, 119334], mahalleler.Where(m => Durum(s, m.Kod, 2019) == SinirDurumu.Supheli).Select(m => m.Kod).Order());
    }
}
