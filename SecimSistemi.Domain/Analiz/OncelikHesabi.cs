namespace SecimSistemi.Domain.Analiz;

/// <summary>Öncelik listesinin bir satırı: mahallenin grubu ve sıralamaya giren değerler.</summary>
/// <param name="Sira">Kendi dönem listesindeki sıra (1'den).</param>
/// <param name="BuyuklukSirasi">Son seçimdeki toplam oya göre ilçedeki sırası (1 = en büyük); toplam oy yoksa boş.</param>
/// <param name="MahalleSayisi">Büyüklük sırasının hesaplandığı mahalle sayısı (ilçenin tamamı).</param>
/// <param name="SupheliYillar">Dönem içinde sınır şüphesi olan seçimler (insan kararı yoksa).</param>
public sealed record OncelikSatiri(int Sira, GrupSatiri Grup, int? BuyuklukSirasi, int MahalleSayisi, string Donem, IReadOnlyList<int> SupheliYillar)
{
    /// <summary>Kayıp oy (beklenen − gerçek); eksi değer kazançtır.</summary>
    public decimal? KayipOy => -Grup.OyDegisimi;
}

/// <summary>Bir karşılaştırılabilir dönemin öncelik listesi.</summary>
public sealed record OncelikListesi(string Donem, int? DonemBaslangici, IReadOnlyList<OncelikSatiri> Satirlar);

/// <summary>
/// Öncelik listesi (docs/kararlar.md K38): her karşılaştırılabilir dönem için ayrı liste (farklı tabanlardan gelen kayıplar
/// tek sıralamada karşılaştırılamaz; K33). Sıra: kayıp oy büyükten küçüğe (kazanç eksi kayıptır, sona düşer), eşitlikte
/// mahalle büyüklüğü (son seçimdeki toplam oy; seçmen sayısı yok), sonra kod. Kayıp oyu hesaplanamayan (DSP pusulada değil)
/// mahalleler listenin sonundadır. Listeler en uzun dönemden başlar; sınır analizinde olmayan mahalleler "—" listesindedir.
/// </summary>
public static class OncelikHesabi
{
    public const string DonemYok = "—";

    public static IReadOnlyList<OncelikListesi> Hesapla(GrupSonucu gruplar, SinirAnaliziSonucu? sinir)
    {
        // Büyüklük sırası ilçenin tamamında (bütün dönemler): "ilçenin 15. büyük mahallesi".
        var buyukluk = gruplar.Satirlar.Where(s => s.SonToplamOy is not null)
            .OrderByDescending(s => s.SonToplamOy).ThenBy(s => s.BirimKod)
            .Select((s, i) => (s.BirimKod, Sira: i + 1))
            .ToDictionary(x => x.BirimKod, x => x.Sira);
        var mahalleSayisi = buyukluk.Count;

        return gruplar.Satirlar
            .GroupBy(s => sinir?.Birim(s.BirimKod) is { } b ? (b.Donem, (int?)b.DonemBaslangici) : (DonemYok, null))
            .OrderBy(g => g.Key.Item2 ?? int.MaxValue)
            .Select(g =>
            {
                var satirlar = g
                    .OrderBy(s => s.OyDegisimi is null)
                    .ThenBy(s => s.OyDegisimi)                 // en çok kaybeden (en eksi değişim) önce
                    .ThenByDescending(s => s.SonToplamOy ?? -1)
                    .ThenBy(s => s.BirimKod)
                    .Select((s, i) =>
                    {
                        var birim = sinir?.Birim(s.BirimKod);
                        var supheli = birim?.SupheliYillar(birim.DonemSonu).Select(d => d.Yil).ToList() ?? [];
                        return new OncelikSatiri(i + 1, s, buyukluk.TryGetValue(s.BirimKod, out var b) ? b : null, mahalleSayisi, g.Key.Item1, supheli);
                    })
                    .ToList();
                return new OncelikListesi(g.Key.Item1, g.Key.Item2, satirlar);
            })
            .ToList();
    }
}
