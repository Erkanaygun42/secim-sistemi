using ClosedXML.Excel;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// <see cref="RaporBelgesi"/>'ni Excel dosyasına yazar. İlk sayfa "Bilgi": "partiye özel" ibaresi, rapor adı, filtre özeti,
/// veri kaynağı özeti, üretim tarihi, "Bu rapor nasıl okunur" (sütun açıklamaları ve örnekleri), notlar ve en sonda yüklenen
/// dosyaların tam listesi. Sonra her tablo ayrı sayfada: sayılar sayı olarak (Excel'de toplanabilir,
/// sıralanabilir), boş değer "—", başlık satırı dondurulmuş ve süzgeçli. Grup satırları (dönem başlıkları) Excel'e yazılmaz;
/// aynı bilgi "Karşılaştırılabilir dönem" sütununda olduğu için süzgeç ve sıralama bozulmaz. Karşılaştırılabilir dönemin
/// dışında kalan hücreler boş ve gridir.
/// </summary>
public static class ExcelYazici
{
    public const string IcerikTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly XLColor UyariRengi = XLColor.FromHtml("#FFF8E1");
    private static readonly XLColor BaslikRengi = XLColor.FromHtml("#F3F3F3");
    public static readonly XLColor DonemDisiRengi = XLColor.FromHtml("#E6E6E6");

    public static byte[] Yaz(RaporBelgesi belge, Gorunum? gorunum = null)
    {
        using var kitap = new XLWorkbook();
        BilgiSayfasi(kitap, belge, gorunum ?? Gorunum.Varsayilan);
        foreach (var bolum in belge.Bolumler)
            TabloSayfasi(kitap, bolum);

        using var akis = new MemoryStream();
        kitap.SaveAs(akis);
        return akis.ToArray();
    }

    private static void BilgiSayfasi(XLWorkbook kitap, RaporBelgesi b, Gorunum gorunum)
    {
        var s = kitap.Worksheets.Add("Bilgi");
        var r = 1;
        s.Cell(r, 1).Value = Metinler.PartiyeOzel;
        s.Range(r, 1, r, 3).Style.Font.SetBold().Font.SetFontSize(14)
            .Font.SetFontColor(XLColor.FromHtml(Gorunum.BantYazisi)).Fill.SetBackgroundColor(XLColor.FromHtml(gorunum.AnaRenk));
        r += 2;

        void Satir(string ad, string deger, string? ucuncu = null)
        {
            s.Cell(r, 1).Value = ad;
            s.Cell(r, 1).Style.Font.SetBold();
            s.Cell(r, 2).Value = deger;
            if (ucuncu is not null) s.Cell(r, 3).Value = ucuncu;
            r++;
        }

        void Baslik(string metin)
        {
            r++;
            s.Cell(r, 1).Value = metin;
            s.Cell(r, 1).Style.Font.SetBold().Font.SetFontSize(12);
            r++;
        }

        Satir("Rapor", b.Baslik);
        Satir("Filtre", b.FiltreOzeti);
        Satir("Veri kaynağı", b.Kaynaklar.Ozet);
        Satir("Üretim tarihi", b.UretimMetni);

        Baslik("Bu rapor nasıl okunur");
        foreach (var t in b.Okuma) Satir(t.Ad, t.Aciklama, t.Ornek is null ? null : "Örnek: " + t.Ornek);

        Baslik("Notlar");
        foreach (var n in b.Notlar) { s.Cell(r, 2).Value = n; r++; }

        Baslik("Yüklenen dosyalar");
        if (b.Kaynaklar.Dosyalar.Count == 0) { s.Cell(r, 2).Value = Metinler.Bos; r++; }
        foreach (var k in b.Kaynaklar.Dosyalar) { s.Cell(r, 2).Value = k; r++; }

        s.Column(1).Width = 26;
        s.Column(2).Width = 90;
        s.Column(3).Width = 60;
        s.Columns(1, 3).Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
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
            var aralik = s.Range(r, 1, r, bolum.Sutunlar.Count);
            if (satir.Tur == SatirTuru.Uyari) aralik.Style.Fill.SetBackgroundColor(UyariRengi);
            for (var c = 0; c < bolum.Sutunlar.Count; c++)
            {
                if (satir.DonemDisiMi(c))
                {
                    s.Cell(r, c + 1).Style.Fill.SetBackgroundColor(DonemDisiRengi);
                    continue;
                }
                Hucre(s.Cell(r, c + 1), bolum.Sutunlar[c].Tur, c < satir.Degerler.Count ? satir.Degerler[c] : null);
            }

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
