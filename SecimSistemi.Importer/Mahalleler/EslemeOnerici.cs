using System.Globalization;

namespace SecimSistemi.Importer.Mahalleler;

/// <summary>Bir mahallenin bir seçimdeki varlığı: sandık sayısı ve belediye meclisi toplam oyu.</summary>
public sealed record MahalleYilVerisi(int SandikSayisi, long MeclisOyu);

/// <summary>Bir mahallenin hangi yerel seçimlerde göründüğü (anahtar: seçim yılı).</summary>
public sealed record MahalleYillari(int Kod, string Ad, IReadOnlyDictionary<int, MahalleYilVerisi> Yillar);

/// <summary>Bir ilçedeki mahallelerin yıllara göre durumu: öneri ve ipucu üretmenin girdisi.</summary>
public sealed record IlceMahalleVerisi(IReadOnlyList<int> Yillar, IReadOnlyList<MahalleYillari> Mahalleler)
{
    /// <summary>Yıl → ilçedeki meclis toplam oyu.</summary>
    public long IlceToplami(int yil) => Mahalleler.Sum(m => m.Yillar.TryGetValue(yil, out var v) ? v.MeclisOyu : 0);

    /// <summary>Mahallenin o yıl ilçe meclis oyundaki payı (0–1); o yıl yoksa boş.</summary>
    public double? Pay(MahalleYillari m, int yil)
    {
        if (!m.Yillar.TryGetValue(yil, out var v)) return null;
        var toplam = IlceToplami(yil);
        return toplam == 0 ? null : (double)v.MeclisOyu / toplam;
    }

    /// <summary>Bütün yıllarda değil, yalnızca bazı yıllarda görünen mahalleler.</summary>
    public IEnumerable<MahalleYillari> EksikYillilar => Mahalleler.Where(m => Yillar.Any(y => !m.Yillar.ContainsKey(y)));
}

/// <summary>
/// Mahalle eşleme önerileri ve ipuçları. Hiçbir şey kaydetmez; çıktı yalnızca "oneri" satırlarıdır.
/// </summary>
public static class EslemeOnerici
{
    public const double EnAzBenzerlik = 0.80;
    public const int MahalleBasinaOneri = 3;

    /// <summary>İpucu eşiği: oy payı en az bu oranda düşen eski mahalle listelenir.</summary>
    public const double PayDususEsigi = 0.25;

    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Sonradan ortaya çıkan her mahalle için daha önce var olan, adı benzeyen mahalleler (eski → yeni); ortadan kalkan
    /// her mahalle için sonradan var olan, adı benzeyen mahalleler. Benzerliği <see cref="EnAzBenzerlik"/>'ten düşük olanlar önerilmez.
    /// </summary>
    public static IReadOnlyList<MahalleEslemeSatiri> Oner(IlceMahalleVerisi veri)
    {
        var oneriler = new List<MahalleEslemeSatiri>();
        int ilkYil = veri.Yillar.Min(), sonYil = veri.Yillar.Max();

        foreach (var m in veri.EksikYillilar)
        {
            int ilk = m.Yillar.Keys.Min(), son = m.Yillar.Keys.Max();
            if (ilk > ilkYil)
            {
                var adaylar = veri.Mahalleler.Where(c => c.Kod != m.Kod && c.Yillar.Keys.Any(y => y < ilk));
                oneriler.AddRange(EnIyiler(m, adaylar).Select(x => Satir(x.Aday, m, x.Benzerlik, veri, ilk)));
            }
            if (son < sonYil)
            {
                var adaylar = veri.Mahalleler.Where(c => c.Kod != m.Kod && c.Yillar.Keys.Any(y => y > son));
                oneriler.AddRange(EnIyiler(m, adaylar).Select(x => Satir(m, x.Aday, x.Benzerlik, veri, x.Aday.Yillar.Keys.Where(y => y > son).Min())));
            }
        }
        return oneriler;
    }

    private static IEnumerable<(MahalleYillari Aday, double Benzerlik)> EnIyiler(MahalleYillari m, IEnumerable<MahalleYillari> adaylar) =>
        adaylar
            .Select(c => (Aday: c, Benzerlik: AdBenzerligi.Benzerlik(m.Ad, c.Ad)))
            .Where(x => x.Benzerlik >= EnAzBenzerlik)
            .OrderByDescending(x => x.Benzerlik).ThenBy(x => x.Aday.Kod)
            .Take(MahalleBasinaOneri);

    private static MahalleEslemeSatiri Satir(MahalleYillari eski, MahalleYillari yeni, double benzerlik, IlceMahalleVerisi veri, int yeniIlkYil)
    {
        var onceki = veri.Yillar.Where(y => y < yeniIlkYil).DefaultIfEmpty().Max();
        var not = $"Ad benzerliği; onaylamadan önce resmî kaynaktan doğrulayın. {PayMetni(veri, eski, onceki, yeniIlkYil)}";
        return new MahalleEslemeSatiri(eski.Kod, eski.Ad, yeni.Kod, yeni.Ad, null, null, EslemeDurumu.Oneri, Math.Round(benzerlik, 2), not);
    }

    private static string PayMetni(IlceMahalleVerisi veri, MahalleYillari m, int y0, int y1)
    {
        string Yuzde(double? p) => p is { } v ? (v * 100).ToString("0.0", Turkce) + "%" : "yok";
        return $"Eski mahallenin ilçe meclis oy payı {y0}: {Yuzde(veri.Pay(m, y0))}, {y1}: {Yuzde(veri.Pay(m, y1))}.";
    }

    /// <summary>
    /// Yeni mahallelerin ortaya çıktığı her seçim için: o seçimde de var olan ama ilçe meclis oy payı belirgin düşen
    /// eski mahalleler. Toprak kaybetmiş (bölünmüş) olabilirler; eşlemeyi elle girerken bakılacak yer burasıdır.
    /// </summary>
    public static IReadOnlyList<string> Ipuclari(IlceMahalleVerisi veri)
    {
        var sonuc = new List<string>();
        var yillar = veri.Yillar.Order().ToList();
        for (int i = 1; i < yillar.Count; i++)
        {
            int y0 = yillar[i - 1], y1 = yillar[i];
            var yeniler = veri.Mahalleler.Where(m => m.Yillar.ContainsKey(y1) && !m.Yillar.ContainsKey(y0)).ToList();
            if (yeniler.Count == 0) continue;

            var dusenler = veri.Mahalleler
                .Where(m => m.Yillar.ContainsKey(y0) && m.Yillar.ContainsKey(y1))
                .Select(m => (M: m, P0: veri.Pay(m, y0) ?? 0, P1: veri.Pay(m, y1) ?? 0))
                .Where(x => x.P0 > 0 && (x.P1 - x.P0) / x.P0 <= -PayDususEsigi)
                .OrderBy(x => (x.P1 - x.P0) / x.P0)
                .ToList();

            sonuc.Add($"{y0} → {y1}: {yeniler.Count} yeni mahalle ortaya çıktı. Oy payı en az %{PayDususEsigi * 100:0} düşen eski mahalleler ({dusenler.Count}):");
            foreach (var (m, p0, p1) in dusenler)
            {
                var s0 = m.Yillar[y0].SandikSayisi;
                var s1 = m.Yillar[y1].SandikSayisi;
                sonuc.Add(string.Create(Turkce,
                    $"  {m.Kod} {m.Ad}: pay %{p0 * 100:0.0} → %{p1 * 100:0.0} ({(p1 - p0) / p0 * 100:+0;-0}%), sandık {s0} → {s1}"));
            }
        }
        return sonuc;
    }
}
