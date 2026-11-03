using System.Globalization;

namespace SecimSistemi.Domain.Analiz;

/// <summary>Birim oluşturmanın girdisi: bir ilçedeki mahalle/köy (Muhtarlık Id ve ad).</summary>
public sealed record BirimMahallesi(int Kod, string Ad);

/// <summary>Onaylı bir eşleme: iki mahalle aynı birime girer. Yön (eski → yeni) birim için önemsizdir.</summary>
public sealed record BirimKenari(int EskiKod, int YeniKod);

public sealed record OlusanBirim(int Kod, string Ad, IReadOnlyList<int> MahalleKodlari);

/// <summary>
/// Karşılaştırma birimlerini kurar: eşlemelerle birbirine bağlı mahalleler (bağlı bileşenler) tek birim olur.
/// Bölünme (A → B, A → C), birleşme (A, B → C) ve zincirler (A → B → C) aynı şekilde ele alınır.
/// </summary>
public static class BirimOlusturucu
{
    private static readonly StringComparer Turkce = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false);

    public static IReadOnlyList<OlusanBirim> Olustur(IReadOnlyCollection<BirimMahallesi> mahalleler, IEnumerable<BirimKenari> kenarlar)
    {
        ArgumentNullException.ThrowIfNull(mahalleler);
        ArgumentNullException.ThrowIfNull(kenarlar);

        var adlar = new Dictionary<int, string>();
        foreach (var m in mahalleler)
        {
            if (!adlar.TryAdd(m.Kod, m.Ad))
                throw new ArgumentException($"{m.Kod} kodlu mahalle iki kez verildi.", nameof(mahalleler));
        }

        // Union-find: her mahalle kendi kümesinin temsilcisiyle başlar.
        var ust = adlar.Keys.ToDictionary(k => k, k => k);
        int Kok(int k)
        {
            while (ust[k] != k)
            {
                ust[k] = ust[ust[k]];
                k = ust[k];
            }
            return k;
        }

        foreach (var kenar in kenarlar)
        {
            foreach (var kod in new[] { kenar.EskiKod, kenar.YeniKod })
            {
                if (!adlar.ContainsKey(kod))
                    throw new ArgumentException($"Eşlemedeki {kod} kodlu mahalle bu ilçede yok (başka ilçeye giden eşleme kurulamaz).", nameof(kenarlar));
            }
            var (a, b) = (Kok(kenar.EskiKod), Kok(kenar.YeniKod));
            if (a != b) ust[Math.Max(a, b)] = Math.Min(a, b);
        }

        return adlar.Keys
            .GroupBy(Kok)
            .Select(g =>
            {
                var kodlar = g.Order().ToList();
                var ad = string.Join(" + ", kodlar.Select(k => adlar[k]).Order(Turkce));
                return new OlusanBirim(kodlar[0], ad, kodlar);
            })
            .OrderBy(b => b.Kod)
            .ToList();
    }
}
