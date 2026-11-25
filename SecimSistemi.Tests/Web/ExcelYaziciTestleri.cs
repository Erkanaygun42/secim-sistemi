using ClosedXML.Excel;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Excel çıktısı: bilgi sayfası, sayı biçimleri, boş değer, uyarı rengi, grup satırları.</summary>
public class ExcelYaziciTestleri
{
    private static XLWorkbook Ac(RaporBelgesi b) => new(new MemoryStream(ExcelYazici.Yaz(b)));

    private static RaporBelgesi Karne() =>
        RaporBelgeleri.Karne(RaporBelgeleriTestleri.OrnekKarne(), RaporBelgeleriTestleri.Kaynaklar, RaporBelgeleriTestleri.Uretim);

    [Fact]
    public void BilgiSayfasi_IbareFiltreKaynakTarih_NasilOkunur_DosyalarSonda()
    {
        using var k = Ac(Karne());
        var s = k.Worksheet(1);

        Assert.Equal("Bilgi", s.Name);
        Assert.Equal("PARTİYE ÖZEL — dağıtılmaz", s.Cell(1, 1).GetString());
        var degerler = s.RangeUsed()!.Rows().Where(r => !r.Cell(1).IsEmpty() && r.RowNumber() > 1)
            .ToDictionary(r => r.Cell(1).GetString(), r => r.Cell(2).GetString());
        Assert.Equal("Mahalle karnesi", degerler["Rapor"]);
        Assert.StartsWith("İSTANBUL / ESENYURT · 2024", degerler["Filtre"]);
        Assert.Equal("YSK Açık Veri Portalı, sandık düzeyi sonuçlar · 2 dosya · son yükleme 3 Kasım 2026", degerler["Veri kaynağı"]);
        Assert.Equal("25 Kasım 2026 14:30 (Türkiye saati)", degerler["Üretim tarihi"]);
        Assert.Equal(SutunSozlugu.Donem.Aciklama, degerler["Karşılaştırılabilir dönem"]);

        // Sıra: nasıl okunur → notlar → dosyaların tam listesi en sonda.
        int Satir(string a) => s.CellsUsed().First(c => c.GetString() == a).Address.RowNumber;
        Assert.True(Satir("Bu rapor nasıl okunur") < Satir("Notlar"));
        Assert.True(Satir("Notlar") < Satir("Yüklenen dosyalar"));
        Assert.Equal(s.LastRowUsed()!.RowNumber(), Satir("2024_yerel_belediye-meclisi_esenyurt.xlsx (yüklendi 3 Kasım 2026)"));
        Assert.Equal(XLColor.FromHtml(Gorunum.VarsayilanRenk), s.Cell(1, 1).Style.Fill.BackgroundColor);
    }

    [Fact]
    public void TabloSayfasi_SayilarSayiOlarak_BicimliVeDondurulmus()
    {
        using var k = Ac(Karne());
        var s = k.Worksheet("Karne");

        Assert.Equal("DSP %", s.Cell(4, 7).GetString());
        Assert.Equal(4, s.SheetView.SplitRow);
        var oy = s.Cell(5, 5);
        Assert.Equal((XLDataType.Number, 12274d, "#,##0"), (oy.DataType, oy.GetDouble(), oy.Style.NumberFormat.Format));
        var oran = s.Cell(5, 7);
        Assert.Equal((0.3177d, "0.00"), (oran.GetDouble(), oran.Style.NumberFormat.Format));
        Assert.Equal((XLDataType.Number, "0"), (s.Cell(5, 2).DataType, s.Cell(5, 2).Style.NumberFormat.Format)); // kod: ayraçsız
        Assert.Equal("İlçe toplamı", s.Cell(7, 3).GetString());
        Assert.True(s.Cell(7, 3).Style.Font.Bold);
        Assert.Equal("A4:K6", s.AutoFilter.Range.RangeAddress.ToString()); // toplam satırı süzgecin dışında
    }

    [Fact]
    public void UyariSatiri_Sari_BosDegerTire()
    {
        var b = Karne();
        var bolum = b.Bolumler[0];
        var bosluklu = bolum with { Satirlar = [bolum.Satirlar[0] with { Degerler = [.. bolum.Satirlar[0].Degerler.Take(5), null, null, .. bolum.Satirlar[0].Degerler.Skip(7)] }] };

        using var k = Ac(b with { Bolumler = [bosluklu] });
        var s = k.Worksheet("Karne");

        Assert.Equal(XLColor.FromHtml("#FFF8E1"), s.Cell(5, 1).Style.Fill.BackgroundColor);
        Assert.Equal("—", s.Cell(5, 6).GetString());
        Assert.Equal("—", s.Cell(5, 7).GetString());
        Assert.StartsWith("2019 seçiminde", s.Cell(5, 11).GetString());
    }

    [Fact]
    public void GrupSatirlariYazilmaz_OndalikYuvarlanir_SayfaAdlariTemiz()
    {
        Sutun[] sutunlar = [new("Ad"), new("Beklenen", HucreTuru.Tamsayi), new("Oran", HucreTuru.Oran)];
        RaporSatiri[] satirlar =
        [
            new(["Dönem 2009–2024"], SatirTuru.Grup),
            new(["A", 1853.0000000000000000000088m, 0.979417951943508m]),
        ];
        var bolum = new TabloBolumu("T", "Seri: [meclis] / başkan ve çok uzun bir sayfa adı", null, sutunlar, satirlar, ["not"]);
        var b = new RaporBelgesi("R", "F", KaynakListesi.Bos, RaporBelgeleriTestleri.Uretim, [bolum, bolum], [], "r");

        using var k = Ac(b);

        var s = k.Worksheet(2);
        Assert.Equal("Seri meclis  başkan ve çok uzun", s.Name);
        Assert.Equal("Seri meclis  başkan ve çok u 2", k.Worksheet(3).Name);
        Assert.Equal("A", s.Cell(5, 1).GetString()); // grup satırı atlandı
        Assert.Equal(1853d, s.Cell(5, 2).GetDouble());
        Assert.Equal(0.9794d, s.Cell(5, 3).GetDouble());
        Assert.Equal("not", s.Cell(7, 1).GetString());
    }

    [Fact]
    public void DonemDisiHucre_BosVeGri()
    {
        var b = RaporBelgeleri.Erime(RaporBelgeleriTestleri.OrnekErime(), KaynakListesi.Bos, RaporBelgeleriTestleri.Uretim);

        using var k = Ac(b);
        var s = k.Worksheet("Mahalle düzeyi");

        Assert.Equal("2009 %", s.Cell(4, 5).GetString());
        Assert.Equal(1d, s.Cell(5, 5).GetDouble());                       // A: 2009 dönem içinde
        Assert.True(s.Cell(6, 5).IsEmpty());                              // B: 2009 dönem dışı → boş
        Assert.Equal(ExcelYazici.DonemDisiRengi, s.Cell(6, 5).Style.Fill.BackgroundColor);
        Assert.Equal(1d, s.Cell(6, 6).GetDouble());
    }
}
