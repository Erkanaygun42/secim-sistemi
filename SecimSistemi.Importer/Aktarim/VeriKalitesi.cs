using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Aktarim;

/// <summary>
/// Yüklemeyi durdurmayan ama dikkat isteyen durumlar (docs/ysk-dosya-yapisi.md, "Veri kalitesi uyarıları").
/// Hiçbir satır silinmez ya da düzeltilmez; yalnızca raporlanır.
/// </summary>
public static class VeriKalitesi
{
    private const int GosterilecekGrup = 5;

    /// <summary>Farklı sandıklara ait, bütün sütunlarda oyları birebir aynı satırlar.</summary>
    public static IReadOnlyList<string> AyniOyluSatirlar(YskSandikDosyasi dosya)
    {
        var gruplar = dosya.Satirlar
            .GroupBy(s => string.Join(",", s.Oylar))
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(s => s.SatirNo).ToList())
            .OrderBy(l => l[0])
            .ToList();
        if (gruplar.Count == 0) return [];

        var yanYana = gruplar.Count(l => l.Zip(l.Skip(1)).Any(c => c.Second == c.First + 1));
        var ornekler = string.Join(" ", gruplar.Take(GosterilecekGrup).Select(l => "[" + string.Join(",", l) + "]"));
        var kalan = gruplar.Count > GosterilecekGrup ? " …" : "";
        return [$"Oyları birebir aynı {gruplar.Count} satır grubu var ({yanYana} tanesinde satırlar yan yana); silinmedi, kontrol edin. Satırlar: {ornekler}{kalan}"];
    }
}
