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
    /// Seçili seride en çok oy kaybeden mahalleler; her karşılaştırılabilir dönem için ayrı grafik, tablodaki dönem
    /// gruplamasıyla aynı sırada (en uzun dönem önce). Farklı dönemlerin kayıpları tek sıralamada karışmaz (K6, K14).
    /// Kayıp oy = beklenen − gerçek. Kaybı olmayan (tabanın üstünde oy alan) mahalleler girmez; kaybı olan mahallesi
    /// olmayan dönemin grafiği yoktur.
    /// </summary>
    public static IReadOnlyList<CubukGrafigi> EnCokKaybedenler(ErimeSonucu e)
    {
        var grafikler = new List<CubukGrafigi>();
        foreach (var grup in e.DonemGruplari().Where(g => g.Key != Metinler.Bos))
        {
            var cubuklar = grup
                .Where(s => s.KayipOy > 0)
                .OrderByDescending(s => s.KayipOy).ThenBy(s => s.BirimKod)
                .Take(KaybedenSayisi)
                .Select(s => new Cubuk(Kisalt(s.BirimAdi, 36), s.KayipOy!.Value,
                    $"{s.BirimAdi} · taban %{Metinler.Oran(s.TabanOrani)} → son %{Metinler.Oran(s.SonOrani)}",
                    e.Uyari(s.BirimKod) is not null))
                .ToList();
            if (cubuklar.Count == 0) continue;
            grafikler.Add(new CubukGrafigi(
                $"En çok oy kaybeden {cubuklar.Count} mahalle · dönem {grup.Key} · {Metinler.Ad(e.OyTuru)}",
                $"{grup.Count()} mahalle içinden. Kayıp oy = beklenen − gerçek; taban {grup.Key[..4]} seçimi. ⚠: sınır şüphesi var.",
                "oy", cubuklar));
        }
        return grafikler;
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
