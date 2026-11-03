using ClosedXML.Excel;
using SecimSistemi.Importer;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Tests.ImporterTestleri;

public class YskDosyaOkuyucuTestleri
{
    /// <summary>Bellekte küçük bir YSK biçimli dosya üretir. Hücre değeri null ise boş bırakılır.</summary>
    internal static MemoryStream Dosya(object?[][] satirlar, Action<XLWorkbook>? ekle = null)
    {
        using var kitap = new XLWorkbook();
        var sayfa = kitap.AddWorksheet("Sheet1");
        for (int r = 0; r < satirlar.Length; r++)
        for (int c = 0; c < satirlar[r].Length; c++)
        {
            if (satirlar[r][c] is { } deger) sayfa.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(deger);
        }
        ekle?.Invoke(kitap);
        var akis = new MemoryStream();
        kitap.SaveAs(akis);
        akis.Position = 0;
        return akis;
    }

    private static readonly object?[] Basliklar = ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "CHP", "DSP"];

    [Fact]
    public void SandikSatirlari_BaslikAdiylaOkunur()
    {
        var akis = Dosya([
            Basliklar,
            [119319, "ARDIÇLI MAH.", 1001, 91, 60, 3],
            ["119319", "ARDIÇLI MAH.", "1002", 80, 61, 1], // sayı yazılı metin de kabul
        ]);

        var dosya = YskDosyaOkuyucu.Oku(akis, "test.xlsx");

        Assert.Equal(["AK PARTİ", "CHP", "DSP"], dosya.OySutunlari.Select(b => b.Ham));
        Assert.Equal([4, 5, 6], dosya.OySutunlari.Select(b => b.SutunNo));
        Assert.Equal(2, dosya.Satirlar.Count);
        var s = dosya.Satirlar[1];
        Assert.Equal((3, 119319, "ARDIÇLI MAH.", 1002), (s.SatirNo, s.MuhtarlikId, s.MuhtarlikAdi, s.SandikNo));
        Assert.Equal([80, 61, 1], s.Oylar);
    }

    [Fact]
    public void KimlikBasligiYanlis_HataVerir()
    {
        var akis = Dosya([["Mahalle", "Muhtarlık Adı", "Sandık No", "CHP"], [1, "A MAH.", 1001, 5]]);

        var hata = Assert.Throws<YuklemeHatasi>(() => YskDosyaOkuyucu.Oku(akis, "test.xlsx"));
        Assert.Contains("A1", hata.Message);
    }

    [Fact]
    public void BozukHucreler_HepsiAdresiyleBildirilir()
    {
        var akis = Dosya([
            Basliklar,
            [1, "A MAH.", 1001, 10, "on", 2],    // E2 sayı değil
            [1, "A MAH.", 1002, 10, 5, null],    // F3 boş
            [1, "A MAH.", 1003, -4, 5, 1],       // D4 negatif
            [1, "A MAH.", 1004, 2.5, 5, 1],      // D5 tam sayı değil
        ]);

        var hata = Assert.Throws<YuklemeHatasi>(() => YskDosyaOkuyucu.Oku(akis, "test.xlsx"));

        Assert.Contains("4 hücre hatası", hata.Message);
        foreach (var adres in new[] { "E2", "F3", "D4", "D5" })
            Assert.Contains(adres + ":", hata.Message);
    }

    [Fact]
    public void AyniSandikIkiKez_HataVerir()
    {
        var akis = Dosya([Basliklar, [1, "A MAH.", 1001, 1, 1, 1], [1, "A MAH.", 1001, 2, 2, 2]]);

        var hata = Assert.Throws<YuklemeHatasi>(() => YskDosyaOkuyucu.Oku(akis, "test.xlsx"));
        Assert.Contains("1001 numaralı sandık 2. satırda da var", hata.Message);
    }

    [Fact]
    public void IkinciSayfa_HataVerir()
    {
        var akis = Dosya([Basliklar, [1, "A MAH.", 1001, 1, 1, 1]], k => k.AddWorksheet("Sayfa2"));

        Assert.Throws<YuklemeHatasi>(() => YskDosyaOkuyucu.Oku(akis, "test.xlsx"));
    }

    [Fact]
    public void YalnizcaBaslik_HataVerir()
    {
        Assert.Throws<YuklemeHatasi>(() => YskDosyaOkuyucu.Oku(Dosya([Basliklar]), "test.xlsx"));
    }
}
