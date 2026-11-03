using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Mahalleler;

/// <summary>
/// Mahalle adlarının benzerliği (0–1). Yalnızca öneri üretmek için kullanılır; hiçbir eşleme benzerliğe bakılarak kaydedilmez.
/// </summary>
public static class AdBenzerligi
{
    private static readonly string[] Ekler = [" MAHALLESİ", " MAH.", " MAH", " KÖYÜ", " KÖY"];

    /// <summary>Türkçe büyük harf, tek boşluk, sondaki "MAH." / "KÖYÜ" eki atılmış ad.</summary>
    public static string Normallestir(string ad)
    {
        var n = PartiAdEslemesi.Normallestir(ad);
        foreach (var ek in Ekler)
        {
            if (n.EndsWith(ek, StringComparison.Ordinal))
                return n[..^ek.Length].TrimEnd();
        }
        return n;
    }

    public static double Benzerlik(string a, string b) => JaroWinkler(Normallestir(a), Normallestir(b));

    /// <summary>Jaro-Winkler benzerliği: ortak harfler ve aynı başlangıç (en çok 4 harf) ödüllendirilir.</summary>
    public static double JaroWinkler(string a, string b)
    {
        if (a == b) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;

        int pencere = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
        var aEslesti = new bool[a.Length];
        var bEslesti = new bool[b.Length];
        int ortak = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int bas = Math.Max(0, i - pencere), son = Math.Min(b.Length - 1, i + pencere);
            for (int j = bas; j <= son; j++)
            {
                if (bEslesti[j] || a[i] != b[j]) continue;
                aEslesti[i] = bEslesti[j] = true;
                ortak++;
                break;
            }
        }
        if (ortak == 0) return 0;

        int yerDegistirme = 0;
        for (int i = 0, j = 0; i < a.Length; i++)
        {
            if (!aEslesti[i]) continue;
            while (!bEslesti[j]) j++;
            if (a[i] != b[j]) yerDegistirme++;
            j++;
        }

        double m = ortak;
        double jaro = (m / a.Length + m / b.Length + (m - yerDegistirme / 2.0) / m) / 3;
        int onEk = 0;
        while (onEk < Math.Min(4, Math.Min(a.Length, b.Length)) && a[onEk] == b[onEk]) onEk++;
        return jaro + onEk * 0.1 * (1 - jaro);
    }
}
