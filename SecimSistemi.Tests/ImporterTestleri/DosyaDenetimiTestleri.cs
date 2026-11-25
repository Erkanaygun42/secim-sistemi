using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Tests.ImporterTestleri;

/// <summary>data/raw indirme denetimi: kontrol listesi, eksik ve yanlış adlı dosyalar, içerik çelişkileri.</summary>
public sealed class DosyaDenetimiTestleri : IDisposable
{
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-denetim-" + Guid.NewGuid().ToString("N"));

    public DosyaDenetimiTestleri() => Directory.CreateDirectory(_klasor);

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    /// <param name="mahalle">İlk mahalle kimliği; dosyadaki sandıklar bu mahalledendir.</param>
    private void Yaz(string ad, int mahalle = 100, int sandik = 2, int oy = 5)
    {
        object?[][] tablo =
        [
            ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "CHP"],
            .. Enumerable.Range(0, sandik).Select(i => new object?[] { mahalle, "A MAH.", 1001 + i, oy + i }),
        ];
        using var akis = YskDosyaOkuyucuTestleri.Dosya(tablo);
        File.WriteAllBytes(Path.Combine(_klasor, ad), akis.ToArray());
    }

    [Fact]
    public void KontrolListesi_DortIlceDortYilUcOyTuru()
    {
        var l = DosyaDenetimi.KontrolListesi();

        Assert.Equal(48, l.Count);
        Assert.Equal(["adalar", "bakirkoy", "esenyurt", "sisli"], l.Select(x => x.Ilce.DosyaAdi).Distinct().Order());
        Assert.Contains(l, x => x.Ad == "2014_yerel_buyuksehir-baskani_bakirkoy.xlsx");
        Assert.DoesNotContain(l, x => x.Ad.Contains("il-genel-meclisi")); // İstanbul büyükşehir: il genel meclisi yok
    }

    [Fact]
    public void TemizKlasor_EksiklerUyari()
    {
        Yaz("2024_yerel_belediye-meclisi_sisli.xlsx", mahalle: 200, oy: 1);
        Yaz("2024_yerel_belediye-baskani_sisli.xlsx", mahalle: 200, oy: 2);

        var s = DosyaDenetimi.Denetle(_klasor);

        Assert.True(s.Temiz);
        Assert.Equal(46, s.Eksikler.Count);
        Assert.DoesNotContain(s.Eksikler, e => e.Ad == "2024_yerel_belediye-meclisi_sisli.xlsx");
    }

    [Theory]
    [InlineData("2024_yerel_belediye-meclisi_Şişli.xlsx", "2024_yerel_belediye-meclisi_sisli.xlsx")]
    [InlineData("2024_Yerel_Belediye-Meclisi_Bakırköy.xlsx", "2024_yerel_belediye-meclisi_bakirkoy.xlsx")]
    [InlineData("2019_yerel_belediye baskani_adalar.xlsx", "2019_yerel_belediye-baskani_adalar.xlsx")]
    [InlineData("Belediye Meclisi Seçim Sonuçları.xlsx", null)]
    [InlineData("2024_yerel_belediye-meclisi_kadikoy.xlsx", null)] // tanımlı ilçe değil
    public void Oner_DogruAdiTahminEder(string ad, string? beklenen) => Assert.Equal(beklenen, DosyaDenetimi.Oner(ad));

    [Fact]
    public void YanlisAd_HataVeOneri_XlsxOlmayanHata_KilitDosyasiYokSayilir()
    {
        Yaz("2024_yerel_belediye-meclisi_Şişli.xlsx");
        File.WriteAllText(Path.Combine(_klasor, "sonuc.csv"), "x");
        File.WriteAllText(Path.Combine(_klasor, "~$2024_yerel_belediye-meclisi_sisli.xlsx"), "kilit");

        var s = DosyaDenetimi.Denetle(_klasor);

        Assert.Equal(2, s.Hatalar.Count);
        Assert.Contains(s.Hatalar, h => h.Contains("Doğru ad büyük olasılıkla: 2024_yerel_belediye-meclisi_sisli.xlsx"));
        Assert.Contains(s.Hatalar, h => h.StartsWith("sonuc.csv: xlsx dosyası değil"));
        Assert.Contains(s.Uyarilar, u => u.Contains("kilit dosyası"));
    }

    [Fact]
    public void AyniIcerik_Hata()
    {
        Yaz("2024_yerel_belediye-meclisi_sisli.xlsx", mahalle: 200);
        File.Copy(Path.Combine(_klasor, "2024_yerel_belediye-meclisi_sisli.xlsx"), Path.Combine(_klasor, "2024_yerel_belediye-baskani_sisli.xlsx"));

        var s = DosyaDenetimi.Denetle(_klasor);

        Assert.Contains(s.Hatalar, h => h.StartsWith("İçeriği aynı dosyalar: 2024_yerel_belediye-baskani_sisli.xlsx, 2024_yerel_belediye-meclisi_sisli.xlsx"));
    }

    [Fact]
    public void BaskaIlceninMahalleleri_Hata()
    {
        Yaz("2024_yerel_belediye-meclisi_esenyurt.xlsx", mahalle: 119318, oy: 1);
        Yaz("2024_yerel_belediye-meclisi_sisli.xlsx", mahalle: 119318, oy: 2); // aslında Esenyurt dosyası

        var s = DosyaDenetimi.Denetle(_klasor);

        Assert.Contains(s.Hatalar, h => h.Contains("'esenyurt' ve 'sisli' dosyalarında 1 ortak mahalle kimliği var"));
    }

    [Fact]
    public void SandikSayisiFarkli_Uyari()
    {
        Yaz("2024_yerel_belediye-meclisi_adalar.xlsx", mahalle: 300, sandik: 2, oy: 1);
        Yaz("2024_yerel_belediye-baskani_adalar.xlsx", mahalle: 300, sandik: 3, oy: 2);

        var s = DosyaDenetimi.Denetle(_klasor);

        Assert.True(s.Temiz);
        Assert.Contains(s.Uyarilar, u => u.StartsWith("2024 adalar: sandık sayıları farklı"));
    }

    [Fact]
    public void Yazdir_TabloVeSonuc()
    {
        Yaz("2024_yerel_belediye-meclisi_sisli.xlsx", mahalle: 200);
        var yazi = new StringWriter();

        DosyaDenetimi.Yazdir(DosyaDenetimi.Denetle(_klasor), yazi);

        Assert.Contains("Kontrol listesi: 48 dosya (4 ilçe × 4 yıl × 3 oy türü", yazi.ToString());
        Assert.Contains("ŞİŞLİ        2009 ···  2014 ···  2019 ···  2024 ✓··", yazi.ToString());
        Assert.Contains("Sonuç: hata yok; 1/48 dosya var.", yazi.ToString());
    }

    [Fact]
    public async Task YukleHepsi_DenetimHatasindaYuklemeBaslamaz()
    {
        Yaz("2024_yerel_belediye-meclisi_Şişli.xlsx");
        var hata = new StringWriter();
        var eski = Console.Error;
        Console.SetError(hata);
        try
        {
            // Veritabanına bağlanmadan döner: denetim hatası yükleme başlamadan yakalanır.
            Assert.Equal(1, await AktarimKomutlari.YukleHepsiAsync(_klasor, yenile: false));
        }
        finally
        {
            Console.SetError(eski);
        }
        Assert.Contains("Yükleme başlamadı", hata.ToString());
    }
}
