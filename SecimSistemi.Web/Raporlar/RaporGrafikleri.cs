using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>Raporların grafik modelleri. Sayfa ve PDF aynı modeli çizer.</summary>
public static class RaporGrafikleri
{
    public const int KaybedenSayisi = 10;

    /// <summary>İlçe düzeyinde yıllara göre DSP oranı; her seri (meclis, başkan, büyükşehir) bir çizgi.</summary>
    public static CizgiGrafigi? IlceOrani(ErimeSonucu e)
    {
        if (e.Ilce is null || e.IlceErimesi.Count == 0) return null;
        var yillar = e.IlceErimesi.Values.SelectMany(v => v.Select(s => s.Yil)).Distinct().Order().ToList();
        var seriler = e.IlceErimesi
            .OrderBy(kv => Metinler.SeriSirasi(kv.Key))
            .Select(kv => new GrafikSerisi(Metinler.Ad(kv.Key), Metinler.SeriSirasi(kv.Key),
                yillar.Select(y => kv.Value.FirstOrDefault(s => s.Yil == y)?.Oran).ToList()))
            .ToList();
        return new CizgiGrafigi($"{e.Ilce.Ad} · yıllara göre DSP oranı",
            "İlçenin tamamı, yüzde. Boşluk: DSP o seçimde o oy türünde kendi listesiyle pusulada değildi.", yillar, seriler);
    }

    /// <summary>
    /// Seçili seride en çok oy kaybeden mahalleler (kayıp oy = beklenen − gerçek, her mahallenin kendi dönemine göre).
    /// Kaybı olmayan (tabanın üstünde oy alan) mahalleler girmez; hiç yoksa boş.
    /// </summary>
    public static CubukGrafigi? EnCokKaybeden(ErimeSonucu e)
    {
        if (e.Rapor is not { } r) return null;
        var cubuklar = r.Satirlar
            .Where(s => s.KayipOy > 0)
            .OrderByDescending(s => s.KayipOy).ThenBy(s => s.BirimKod)
            .Take(KaybedenSayisi)
            .Select(s =>
            {
                var donem = e.Sinir?.Birim(s.BirimKod)?.Donem ?? Metinler.Bos;
                return new Cubuk($"{Kisalt(s.BirimAdi, 26)} ({donem})", s.KayipOy!.Value,
                    $"{s.BirimAdi} · dönem {donem} · taban %{Metinler.Oran(s.TabanOrani)} → son %{Metinler.Oran(s.SonOrani)}",
                    e.Uyari(s.BirimKod) is not null);
            })
            .ToList();
        if (cubuklar.Count == 0) return null;
        return new CubukGrafigi($"En çok oy kaybeden {cubuklar.Count} mahalle · {Metinler.Ad(e.OyTuru)}",
            "Kayıp oy = beklenen − gerçek; her mahalle kendi karşılaştırılabilir dönemine (parantez içinde) göre. " +
            "Dönemi kısa olan mahallenin kaybı daha kısa bir sürede oluşmuştur. ⚠: sınır şüphesi var.",
            "oy", cubuklar);
    }

    /// <summary>Uzun mahalle adı çubuk etiketine sığsın diye kısaltılır (tam ad ipucunda).</summary>
    private static string Kisalt(string ad, int enCok) => ad.Length <= enCok ? ad : ad[..(enCok - 1)].TrimEnd() + "…";

    /// <summary>Mahallede yıllara göre DSP oranı; her oy türü bir çizgi. Karşılaştırılabilir dönemin dışı gri bantla gösterilir.</summary>
    public static CizgiGrafigi? MahalleOrani(MahalleDetayi d)
    {
        if (d.Satirlar.Count == 0) return null;
        var yillar = d.Satirlar.Select(s => s.Yil).Distinct().Order().ToList();
        var seriler = d.Satirlar.GroupBy(s => s.OyTuru)
            .OrderBy(g => Metinler.SeriSirasi(g.Key))
            .Select(g => new GrafikSerisi(Metinler.Ad(g.Key), Metinler.SeriSirasi(g.Key),
                yillar.Select(y => g.FirstOrDefault(s => s.Yil == y)?.DspOrani).ToList()))
            .ToList();
        int? donem = d.Sinir is { } s && s.DonemBaslangici > yillar[0] ? s.DonemBaslangici : null;
        return new CizgiGrafigi($"{d.BirimAdi} · yıllara göre DSP oranı",
            "Yüzde." + (donem is { } b ? $" Gri bant: karşılaştırılabilir dönemin ({b} öncesi) dışı; o yıllar aynı toprağı göstermez." : ""),
            yillar, seriler, donem);
    }
}
