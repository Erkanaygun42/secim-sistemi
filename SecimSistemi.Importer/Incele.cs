using ClosedXML.Excel;

namespace SecimSistemi.Importer;

/// <summary>
/// GEÇİCİ: YSK Excel dosyalarının yapısını yazdırır. Dosyalar yalnızca okunur, hiç kaydedilmez.
/// Kullanım:
///   dotnet run --project SecimSistemi.Importer -- incele data\raw
///   dotnet run --project SecimSistemi.Importer -- karsilastir a.xlsx b.xlsx
/// </summary>
internal static class Incele
{
    private const int BasSatir = 12;
    private const int SonSatir = 5;

    public static void Klasor(string klasor)
    {
        foreach (var yol in Directory.GetFiles(klasor, "*.xlsx").Order())
        {
            Dosya(yol);
        }
    }

    private static XLWorkbook SaltOkunurAc(string yol)
    {
        // FileAccess.Read: dosyaya yazma izni istenmez.
        using var akis = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bellek = new MemoryStream();
        akis.CopyTo(bellek);
        bellek.Position = 0;
        return new XLWorkbook(bellek);
    }

    private static void Dosya(string yol)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 100));
        Console.WriteLine($"DOSYA: {Path.GetFileName(yol)}");
        using var kitap = SaltOkunurAc(yol);
        Console.WriteLine($"Sayfalar: {string.Join(" | ", kitap.Worksheets.Select(s => s.Name))}");

        foreach (var sayfa in kitap.Worksheets)
        {
            var aralik = sayfa.RangeUsed();
            Console.WriteLine();
            Console.WriteLine($"--- Sayfa: {sayfa.Name}");
            if (aralik is null)
            {
                Console.WriteLine("(boş)");
                continue;
            }

            int ilkSatir = aralik.FirstRow().RowNumber();
            int sonSatir = aralik.LastRow().RowNumber();
            int ilkSutun = aralik.FirstColumn().ColumnNumber();
            int sonSutun = aralik.LastColumn().ColumnNumber();
            Console.WriteLine($"Kullanılan aralık: {aralik.RangeAddress}  satır {ilkSatir}-{sonSatir} ({sonSatir - ilkSatir + 1}), sütun {ilkSutun}-{sonSutun} ({sonSutun - ilkSutun + 1})");

            var birlesik = sayfa.MergedRanges.Select(r => r.RangeAddress.ToString()).ToList();
            Console.WriteLine($"Birleştirilmiş hücreler ({birlesik.Count}): {string.Join(", ", birlesik.Take(30))}{(birlesik.Count > 30 ? " ..." : "")}");

            Console.WriteLine($"İlk {BasSatir} satır:");
            for (int r = ilkSatir; r <= Math.Min(sonSatir, ilkSatir + BasSatir - 1); r++)
                SatirYaz(sayfa, r, ilkSutun, sonSutun);

            Console.WriteLine($"Son {SonSatir} satır:");
            for (int r = Math.Max(ilkSatir + BasSatir, sonSatir - SonSatir + 1); r <= sonSatir; r++)
                SatirYaz(sayfa, r, ilkSutun, sonSutun);
        }
    }

    private static void SatirYaz(IXLWorksheet sayfa, int satir, int ilkSutun, int sonSutun)
    {
        var hucreler = new List<string>();
        for (int c = ilkSutun; c <= sonSutun; c++)
        {
            var deger = sayfa.Cell(satir, c).GetFormattedString().Replace("\n", "⏎").Trim();
            hucreler.Add(deger.Length > 40 ? deger[..40] + "…" : deger);
        }
        Console.WriteLine($"  [{satir}] {string.Join(" ¦ ", hucreler)}");
    }

    /// <summary>Her dosya için sayısal özet: mahalle, sandık, tekrarlar, sayı olmayan hücreler, DSP toplamı.</summary>
    public static void Ozet(string klasor)
    {
        foreach (var yol in Directory.GetFiles(klasor, "*.xlsx").Order())
        {
            using var kitap = SaltOkunurAc(yol);
            var s = kitap.Worksheet(1);
            var aralik = s.RangeUsed()!;
            int sonSatir = aralik.LastRow().RowNumber();
            int sonSutun = aralik.LastColumn().ColumnNumber();
            var basliklar = Enumerable.Range(1, sonSutun).Select(c => s.Cell(1, c).GetString().Trim()).ToList();
            int dspSutun = basliklar.IndexOf("DSP") + 1;

            var mahalleler = new HashSet<string>();
            var sandiklar = new Dictionary<string, int>();
            var satirImzalari = new Dictionary<string, List<int>>();
            int sayiDegil = 0, bos = 0;
            long toplamOy = 0, dsp = 0;

            for (int r = 2; r <= sonSatir; r++)
            {
                var mid = s.Cell(r, 1).GetString().Trim();
                var sno = s.Cell(r, 3).GetString().Trim();
                mahalleler.Add(mid);
                var anahtar = $"{mid}/{sno}";
                sandiklar[anahtar] = sandiklar.GetValueOrDefault(anahtar) + 1;

                var degerler = new List<string>();
                for (int c = 4; c <= sonSutun; c++)
                {
                    var h = s.Cell(r, c);
                    if (h.IsEmpty()) { bos++; degerler.Add(""); continue; }
                    if (h.TryGetValue(out double d)) { toplamOy += (long)d; if (c == dspSutun) dsp += (long)d; }
                    else sayiDegil++;
                    degerler.Add(h.GetString());
                }
                var imza = string.Join(",", degerler);
                if (!satirImzalari.TryGetValue(imza, out var liste)) satirImzalari[imza] = liste = [];
                liste.Add(r);
            }

            var tekrarSandik = sandiklar.Where(k => k.Value > 1).ToList();
            var ayniOySatirlari = satirImzalari.Values.Where(l => l.Count > 1).ToList();
            Console.WriteLine($"{Path.GetFileName(yol)}");
            Console.WriteLine($"  veri satırı {sonSatir - 1}, sütun {sonSutun} (3 kimlik + {sonSutun - 3} parti/aday), mahalle {mahalleler.Count}, farklı (mahalle,sandık) {sandiklar.Count}");
            Console.WriteLine($"  tekrar eden sandık: {tekrarSandik.Count} {string.Join(" ", tekrarSandik.Take(5).Select(k => k.Key + "×" + k.Value))}");
            Console.WriteLine($"  oyları birebir aynı satır grupları: {ayniOySatirlari.Count} {string.Join(" ", ayniOySatirlari.Take(5).Select(l => "[" + string.Join(",", l) + "]"))}");
            Console.WriteLine($"  boş hücre {bos}, sayı olmayan hücre {sayiDegil}, toplam oy {toplamOy:N0}, DSP {(dspSutun > 0 ? dsp.ToString("N0") : "sütun yok")}");
            Console.WriteLine($"  sandık no aralığı: {sandiklar.Keys.Select(k => int.TryParse(k.Split('/')[1], out var n) ? n : 0).Min()}-{sandiklar.Keys.Select(k => int.TryParse(k.Split('/')[1], out var n) ? n : 0).Max()}");
        }
    }

    public static void Karsilastir(string a, string b)
    {
        using var ka = SaltOkunurAc(a);
        using var kb = SaltOkunurAc(b);
        Console.WriteLine($"A: {Path.GetFileName(a)}  sayfalar: {string.Join(" | ", ka.Worksheets.Select(s => s.Name))}");
        Console.WriteLine($"B: {Path.GetFileName(b)}  sayfalar: {string.Join(" | ", kb.Worksheets.Select(s => s.Name))}");

        int toplamFark = 0;
        foreach (var sa in ka.Worksheets)
        {
            if (!kb.TryGetWorksheet(sa.Name, out var sb))
            {
                Console.WriteLine($"B'de '{sa.Name}' sayfası yok.");
                continue;
            }
            var ra = sa.RangeUsed();
            var rb = sb.RangeUsed();
            Console.WriteLine($"Sayfa '{sa.Name}': A aralık {ra?.RangeAddress}, B aralık {rb?.RangeAddress}");
            if (ra is null || rb is null) continue;

            int sonSatir = Math.Max(ra.LastRow().RowNumber(), rb.LastRow().RowNumber());
            int sonSutun = Math.Max(ra.LastColumn().ColumnNumber(), rb.LastColumn().ColumnNumber());
            int sayfaFark = 0;
            for (int r = 1; r <= sonSatir; r++)
            for (int c = 1; c <= sonSutun; c++)
            {
                var va = sa.Cell(r, c).GetFormattedString();
                var vb = sb.Cell(r, c).GetFormattedString();
                if (va == vb) continue;
                sayfaFark++;
                if (sayfaFark <= 25)
                    Console.WriteLine($"  {sa.Cell(r, c).Address}: A='{va}'  B='{vb}'");
            }
            Console.WriteLine($"  Farklı hücre sayısı: {sayfaFark}");
            toplamFark += sayfaFark;
        }
        Console.WriteLine(toplamFark == 0 ? "SONUÇ: İçerik birebir aynı." : $"SONUÇ: {toplamFark} hücre farklı.");
    }
}
