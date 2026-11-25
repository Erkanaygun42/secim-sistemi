using ClosedXML.Excel;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// <see cref="RaporBelgesi"/>'ni Excel dosyasına yazar. İlk sayfa "Bilgi": "partiye özel" ibaresi, rapor adı, filtre özeti,
/// veri kaynağı ve dosyaları, üretim tarihi, notlar. Sonra her tablo ayrı sayfada: sayılar sayı olarak (Excel'de toplanabilir,
/// sıralanabilir), boş değer "—", başlık satırı dondurulmuş ve süzgeçli. Grup satırları (dönem başlıkları) Excel'e yazılmaz;
/// aynı bilgi "Dönem" sütununda olduğu için süzgeç ve sıralama bozulmaz.
/// </summary>
public static class ExcelYazici
{
    public const string IcerikTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly XLColor UyariRengi = XLColor.FromHtml("#FFF8E1");
    private static readonly XLColor BaslikRengi = XLColor.FromHtml("#F3F3F3");

    public static byte[] Yaz(RaporBelgesi belge)
    {
        using var kitap = new XLWorkbook();
        BilgiSayfasi(kitap, belge);
        foreach (var bolum in belge.Bolumler)
            TabloSayfasi(kitap, bolum);

        using var akis = new MemoryStream();
        kitap.SaveAs(akis);
        return akis.ToArray();
    }

    private static void BilgiSayfasi(XLWorkbook kitap, RaporBelgesi b)
    {
        var s = kitap.Worksheets.Add("Bilgi");
        var r = 1;
        s.Cell(r, 1).Value = Metinler.PartiyeOzel;
        s.Cell(r, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#C8102E")).Font.SetFontSize(14);
        r += 2;

        void Satir(string ad, string deger)
        {
            s.Cell(r, 1).Value = ad;
            s.Cell(r, 1).Style.Font.SetBold();
            s.Cell(r, 2).Value = deger;
            r++;
        }

        Satir("Rapor", b.Baslik);
        Satir("Filtre", b.FiltreOzeti);
        Satir("Veri kaynağı", Metinler.VeriKaynagi);
        Satir("Üretim tarihi", b.UretimMetni);
        r++;
        Satir("Yüklenen dosyalar", b.Kaynaklar.Count == 0 ? Metinler.Bos : b.Kaynaklar[0]);
        foreach (var k in b.Kaynaklar.Skip(1)) { s.Cell(r, 2).Value = k; r++; }
        r++;
        Satir("Notlar", b.Notlar.Count == 0 ? Metinler.Bos : b.Notlar[0]);
        foreach (var n in b.Notlar.Skip(1)) { s.Cell(r, 2).Value = n; r++; }

        s.Column(1).Width = 20;
        s.Column(2).Width = 120;
        s.Column(2).Style.Alignment.SetWrapText();
    }

    private static void TabloSayfasi(XLWorkbook kitap, TabloBolumu bolum)
    {
        var s = kitap.Worksheets.Add(SayfaAdi(kitap, bolum.SayfaAdi));
        s.Cell(1, 1).Value = bolum.Baslik;
        s.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        if (bolum.Aciklama is not null) s.Cell(2, 1).Value = bolum.Aciklama;

        const int baslikSatiri = 4;
        for (var c = 0; c < bolum.Sutunlar.Count; c++)
        {
            var h = s.Cell(baslikSatiri, c + 1);
            h.Value = bolum.Sutunlar[c].Baslik;
            h.Style.Font.SetBold().Fill.SetBackgroundColor(BaslikRengi);
            if (bolum.Sutunlar[c].Tur != HucreTuru.Metin) h.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }

        var r = baslikSatiri + 1;
        foreach (var satir in bolum.Satirlar.Where(x => x.Tur != SatirTuru.Grup))
        {
            for (var c = 0; c < bolum.Sutunlar.Count; c++)
                Hucre(s.Cell(r, c + 1), bolum.Sutunlar[c].Tur, c < satir.Degerler.Count ? satir.Degerler[c] : null);

            var aralik = s.Range(r, 1, r, bolum.Sutunlar.Count);
            if (satir.Tur == SatirTuru.Uyari) aralik.Style.Fill.SetBackgroundColor(UyariRengi);
            if (satir.Tur == SatirTuru.Toplam) aralik.Style.Font.SetBold();
            r++;
        }

        // Süzgeç yalnızca veri satırlarında; toplam satırı varsa süzgecin dışında kalır.
        var sonVeri = bolum.Satirlar.Any(x => x.Tur == SatirTuru.Toplam) ? r - 2 : r - 1;
        if (sonVeri > baslikSatiri) s.Range(baslikSatiri, 1, sonVeri, bolum.Sutunlar.Count).SetAutoFilter();
        s.SheetView.FreezeRows(baslikSatiri);

        r++;
        foreach (var n in bolum.Notlar) { s.Cell(r, 1).Value = n; r++; }

        s.Columns(1, bolum.Sutunlar.Count).AdjustToContents(baslikSatiri, r);
        for (var c = 1; c <= bolum.Sutunlar.Count; c++)
            if (s.Column(c).Width > 60) { s.Column(c).Width = 60; s.Column(c).Style.Alignment.SetWrapText(); }
    }

    private static void Hucre(IXLCell h, HucreTuru tur, object? deger)
    {
        if (deger is null)
        {
            h.Value = tur == HucreTuru.Metin ? "" : Metinler.Bos;
            if (tur != HucreTuru.Metin) h.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            return;
        }

        switch (deger)
        {
            case string m: h.Value = m; return;
            case int i: h.Value = i; break;
            case long l: h.Value = l; break;
            // Beklenen ve kayıp oy hesaptan ondalıklı gelir (taban oranı × toplam oy); Excel'e tam sayı yazılır.
            // Oranlar 4 ondalıkla yazılır (ekranda 2): hesap artıkları (8,8E-24 gibi) dosyaya girmez.
            case decimal d: h.Value = Math.Round(d, tur == HucreTuru.Tamsayi ? 0 : 4, MidpointRounding.AwayFromZero); break;
            case double x: h.Value = x; break;
            default: h.Value = Convert.ToString(deger, Metinler.Turkce); return;
        }

        // Biçim kodları Excel'de dil ayarına göre gösterilir: tr-TR'de 12.345 ve 0,98.
        h.Style.NumberFormat.Format = tur switch
        {
            HucreTuru.Tamsayi => "#,##0",
            HucreTuru.Oran or HucreTuru.Katsayi => "0.00",
            HucreTuru.Puan => "+0.00;-0.00;0.00",
            _ => "0",
        };
    }

    /// <summary>Excel sayfa adı kuralları: en çok 31 karakter, : \ / ? * [ ] yok, benzersiz.</summary>
    private static string SayfaAdi(XLWorkbook kitap, string ad)
    {
        var temiz = new string(ad.Where(c => !":\\/?*[]".Contains(c)).ToArray());
        if (temiz.Length > 31) temiz = temiz[..31];
        var aday = temiz;
        for (var i = 2; kitap.Worksheets.Contains(aday); i++)
            aday = (temiz.Length > 28 ? temiz[..28] : temiz) + " " + i;
        return aday;
    }
}
