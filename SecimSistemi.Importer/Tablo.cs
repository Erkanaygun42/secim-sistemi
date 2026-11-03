using System.Globalization;

namespace SecimSistemi.Importer;

/// <summary>Ekrana hizalı metin tablosu yazar. Türkçe karakterlerin genişliği tek sütun sayılır.</summary>
internal static class Tablo
{
    public static void Yaz(IReadOnlyList<string> basliklar, IReadOnlyList<IReadOnlyList<string>> satirlar, ISet<int>? sagaYasli = null)
    {
        var genislik = basliklar.Select((b, i) => Math.Max(Uzunluk(b), satirlar.Count == 0 ? 0 : satirlar.Max(s => Uzunluk(s[i])))).ToArray();

        string Satir(IReadOnlyList<string> hucreler) => string.Join("  ", hucreler.Select((h, i) =>
            sagaYasli?.Contains(i) == true ? h.PadLeft(genislik[i]) : h.PadRight(genislik[i])));

        Console.WriteLine(Satir(basliklar));
        Console.WriteLine(string.Join("  ", genislik.Select(g => new string('-', g))));
        foreach (var s in satirlar)
            Console.WriteLine(Satir(s));
    }

    private static int Uzunluk(string s) => new StringInfo(s).LengthInTextElements;
}
