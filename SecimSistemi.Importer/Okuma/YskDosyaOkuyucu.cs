using System.Globalization;
using ClosedXML.Excel;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Okuma;

/// <summary>Bir sütun başlığı ve sütunun Excel'deki numarası (A = 1).</summary>
public sealed record Baslik(int SutunNo, string Ham);

/// <summary>Bir sandık satırı. <see cref="Oylar"/>, <see cref="YskSandikDosyasi.OySutunlari"/> ile aynı sıradadır.</summary>
public sealed record SandikSatiri(int SatirNo, int MuhtarlikId, string MuhtarlikAdi, int SandikNo, IReadOnlyList<int> Oylar);

public sealed record YskSandikDosyasi(string DosyaAdi, IReadOnlyList<Baslik> OySutunlari, IReadOnlyList<SandikSatiri> Satirlar);

/// <summary>
/// YSK sandık dosyasını okur (yapı: docs/ysk-dosya-yapisi.md). Tek sayfa, 1. satır başlık, her satır bir sandık;
/// ilk üç sütun Muhtarlık Id, Muhtarlık Adı, Sandık No; sonraki her sütun bir parti ya da aday.
/// Sütunlar sırayla değil başlık adıyla tanınır. Dosya salt okunur açılır, hiçbir zaman kaydedilmez.
/// Bozuk hücreler tek tek toplanır ve hepsi birlikte bildirilir.
/// </summary>
public static class YskDosyaOkuyucu
{
    private static readonly string[] KimlikBasliklari = ["MUHTARLIK ID", "MUHTARLIK ADI", "SANDIK NO"];
    private const int GosterilecekHata = 20;

    public static YskSandikDosyasi Oku(string yol)
    {
        // FileAccess.Read: dosyaya yazma izni istenmez.
        using var akis = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Oku(akis, Path.GetFileName(yol));
    }

    public static YskSandikDosyasi Oku(Stream akis, string dosyaAdi)
    {
        using var bellek = new MemoryStream();
        akis.CopyTo(bellek);
        bellek.Position = 0;
        using var kitap = new XLWorkbook(bellek);

        if (kitap.Worksheets.Count != 1)
            throw new YuklemeHatasi($"{dosyaAdi}: tek sayfa bekleniyordu, {kitap.Worksheets.Count} sayfa var ({string.Join(", ", kitap.Worksheets.Select(s => s.Name))}).");

        var sayfa = kitap.Worksheet(1);
        var aralik = sayfa.RangeUsed() ?? throw new YuklemeHatasi($"{dosyaAdi}: sayfa boş.");
        if (aralik.FirstRow().RowNumber() != 1 || aralik.FirstColumn().ColumnNumber() != 1)
            throw new YuklemeHatasi($"{dosyaAdi}: veri A1 hücresinde başlamıyor ({aralik.RangeAddress}).");

        int sonSatir = aralik.LastRow().RowNumber();
        int sonSutun = aralik.LastColumn().ColumnNumber();

        for (int c = 1; c <= KimlikBasliklari.Length; c++)
        {
            var baslik = sayfa.Cell(1, c).GetString();
            if (PartiAdEslemesi.Normallestir(baslik) != KimlikBasliklari[c - 1])
                throw new YuklemeHatasi($"{dosyaAdi}: {sayfa.Cell(1, c).Address} başlığı '{baslik}'; beklenen 'Muhtarlık Id', 'Muhtarlık Adı', 'Sandık No'.");
        }
        if (sonSutun <= KimlikBasliklari.Length)
            throw new YuklemeHatasi($"{dosyaAdi}: parti ya da aday sütunu yok.");

        var oySutunlari = Enumerable.Range(KimlikBasliklari.Length + 1, sonSutun - KimlikBasliklari.Length)
            .Select(c => new Baslik(c, sayfa.Cell(1, c).GetString().Trim()))
            .ToList();

        var hatalar = new List<string>();
        var satirlar = new List<SandikSatiri>(sonSatir - 1);
        var gorulenSandiklar = new Dictionary<(int Mahalle, int Sandik), int>();

        for (int r = 2; r <= sonSatir; r++)
        {
            int oncekiHata = hatalar.Count;
            var mahalleId = TamSayi(sayfa.Cell(r, 1), hatalar);
            var mahalleAdi = sayfa.Cell(r, 2).GetString().Trim();
            if (mahalleAdi.Length == 0) hatalar.Add($"{sayfa.Cell(r, 2).Address}: mahalle/köy adı boş");
            var sandikNo = TamSayi(sayfa.Cell(r, 3), hatalar);

            var oylar = new int[oySutunlari.Count];
            for (int i = 0; i < oySutunlari.Count; i++)
                oylar[i] = TamSayi(sayfa.Cell(r, oySutunlari[i].SutunNo), hatalar) ?? 0;

            if (hatalar.Count > oncekiHata) continue;

            if (!gorulenSandiklar.TryAdd((mahalleId!.Value, sandikNo!.Value), r))
            {
                hatalar.Add($"{r}. satır: {mahalleId} kimlikli mahalle/köydeki {sandikNo} numaralı sandık {gorulenSandiklar[(mahalleId.Value, sandikNo.Value)]}. satırda da var");
                continue;
            }
            satirlar.Add(new SandikSatiri(r, mahalleId.Value, mahalleAdi, sandikNo.Value, oylar));
        }

        if (hatalar.Count > 0)
        {
            var liste = string.Join(Environment.NewLine, hatalar.Take(GosterilecekHata).Select(h => "  " + h));
            var kalan = hatalar.Count > GosterilecekHata ? $"{Environment.NewLine}  … ve {hatalar.Count - GosterilecekHata} hata daha" : "";
            throw new YuklemeHatasi($"{dosyaAdi}: {hatalar.Count} hücre hatası var, dosya yüklenmedi:{Environment.NewLine}{liste}{kalan}");
        }
        if (satirlar.Count == 0)
            throw new YuklemeHatasi($"{dosyaAdi}: başlık satırından sonra veri yok.");

        return new YskSandikDosyasi(dosyaAdi, oySutunlari, satirlar);
    }

    /// <summary>Negatif olmayan tam sayı okur; sayı ya da sayı yazılı metin kabul edilir.</summary>
    private static int? TamSayi(IXLCell hucre, List<string> hatalar)
    {
        if (hucre.IsEmpty())
        {
            hatalar.Add($"{hucre.Address}: boş hücre");
            return null;
        }
        if (hucre.DataType == XLDataType.Number)
        {
            var d = hucre.GetDouble();
            if (d >= 0 && d <= int.MaxValue && d == Math.Floor(d)) return (int)d;
        }
        else if (hucre.DataType == XLDataType.Text &&
                 int.TryParse(hucre.GetString().Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }
        hatalar.Add($"{hucre.Address}: '{hucre.GetFormattedString()}' negatif olmayan bir tam sayı değil");
        return null;
    }
}
