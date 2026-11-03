using System.Globalization;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Importer;

/// <summary>
/// Komut satırı: konumsal değerler ve "--ad değer" seçenekleri. Değersiz bayraklar ayrıca tanımlıdır.
/// Tanımsız seçenek hata verir (yazım hatası sessizce yok sayılmasın).
/// </summary>
internal sealed class Secenekler
{
    private static readonly HashSet<string> Bayraklar = ["--yenile", "--reddet", "--hepsi"];
    private static readonly HashSet<string> Degerliler =
    [
        "--oy-turu", "--dusus-esigi", "--artis-esigi", "--supheli-esigi",
        "--mahalle", "--secim", "--karar", "--esle", "--tur", "--yururluk", "--gerekce", "--kaynak", "--kisi", "--not",
    ];

    private readonly Dictionary<string, string> _degerler = [];
    private readonly HashSet<string> _bayraklar = [];

    public IReadOnlyList<string> Konumsal { get; }

    public Secenekler(string[] args)
    {
        var konumsal = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (Bayraklar.Contains(a)) _bayraklar.Add(a);
            else if (Degerliler.Contains(a))
                _degerler[a] = i + 1 < args.Length ? args[++i] : throw new YuklemeHatasi($"{a} bir değer ister.");
            else if (a.StartsWith("--", StringComparison.Ordinal)) throw new YuklemeHatasi($"Bilinmeyen seçenek: {a}");
            else konumsal.Add(a);
        }
        Konumsal = konumsal;
    }

    public bool Bayrak(string ad) => _bayraklar.Contains(ad);

    public string? Deger(string ad) => _degerler.GetValueOrDefault(ad);

    public string Zorunlu(string ad, string aciklama) =>
        Deger(ad) is { Length: > 0 } d ? d : throw new YuklemeHatasi($"{ad} gerekli: {aciklama}.");

    public int Tamsayi(string ad, string aciklama) =>
        int.TryParse(Zorunlu(ad, aciklama), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n : throw new YuklemeHatasi($"{ad} bir tam sayı olmalı ({aciklama}).");

    /// <summary>Sınır eşikleri: verilmeyen eşik varsayılan kalır. Değerler oran olarak (0.25 = %25), ondalık ayracı nokta.</summary>
    public SinirEsikleri Esikler()
    {
        var v = SinirEsikleri.Varsayilan;
        decimal Oku(string ad, decimal varsayilan) => Deger(ad) is not { } d ? varsayilan
            : decimal.TryParse(d, NumberStyles.Number, CultureInfo.InvariantCulture, out var x) ? x
            : throw new YuklemeHatasi($"{ad} '{d}' bir oran olmalı (örnek 0.25).");

        var e = new SinirEsikleri(Oku("--dusus-esigi", v.Dusus), Oku("--artis-esigi", v.Artis), Oku("--supheli-esigi", v.Supheli));
        try
        {
            e.Dogrula();
        }
        catch (ArgumentOutOfRangeException hata)
        {
            throw new YuklemeHatasi($"Geçersiz eşik: {hata.Message}");
        }
        return e;
    }
}
