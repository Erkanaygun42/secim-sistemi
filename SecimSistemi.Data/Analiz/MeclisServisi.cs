using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Data.Analiz;

/// <summary>Bir ilçenin bir yerel seçimdeki belediye meclisi asıl üye sayısı ve bu sayının güvenilirliği.</summary>
/// <param name="Dogrulandi">YSK'nın ilan ettiği sonuçtan okundu mu; değilse varsayımdır, sayfada uyarılır.</param>
public sealed record MeclisUyeSayisi(string IlceKod, int Yil, int UyeSayisi, bool Dogrulandi, string Kaynak);

/// <summary>
/// <c>Tanimlar/meclis-uye-sayilari.csv</c>: ilçe ve yıl başına belediye meclisi üye sayısı. Nüfus verisi olmadığı için
/// üye sayısı nüfustan hesaplanmaz, ilan edilen sayı elle girilir (docs/kararlar.md K36).
/// Biçim: <c>ilce;yil;uye_sayisi;durum;kaynak</c>, durum <c>dogrulandi</c> ya da <c>dogrulanmadi</c>; # ile başlayan satır yorumdur.
/// </summary>
public static class MeclisUyeSayilari
{
    public const string DosyaAdi = "meclis-uye-sayilari.csv";

    public static IReadOnlyList<MeclisUyeSayisi> Oku(string yol) => Coz(File.ReadAllLines(yol), Path.GetFileName(yol));

    public static IReadOnlyList<MeclisUyeSayisi> Coz(IEnumerable<string> satirlar, string kaynakAdi = DosyaAdi)
    {
        var sonuc = new List<MeclisUyeSayisi>();
        var baslikGeldi = false;
        var no = 0;
        foreach (var ham in satirlar)
        {
            no++;
            var satir = ham.Trim();
            if (satir.Length == 0 || satir.StartsWith('#')) continue;
            var p = satir.Split(';', 5); // kaynak (son alan) serbest metin: noktalı virgül içerebilir
            if (!baslikGeldi)
            {
                if (string.Join(";", p.Select(x => x.Trim())) != "ilce;yil;uye_sayisi;durum;kaynak")
                    throw new FormatException($"{kaynakAdi}:{no}: başlık 'ilce;yil;uye_sayisi;durum;kaynak' olmalı.");
                baslikGeldi = true;
                continue;
            }
            if (p.Length != 5) throw new FormatException($"{kaynakAdi}:{no}: 5 alan olmalı, {p.Length} var.");
            if (!int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out var yil))
                throw new FormatException($"{kaynakAdi}:{no}: yıl sayı olmalı: '{p[1]}'.");
            if (!int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out var uye))
                throw new FormatException($"{kaynakAdi}:{no}: üye sayısı sayı olmalı: '{p[2]}'.");
            try { MeclisHesabi.KontenjanSayisi(uye); }
            catch (ArgumentOutOfRangeException) { throw new FormatException($"{kaynakAdi}:{no}: üye sayısı {uye} 2972 m.5/b'deki sayılardan biri değil."); }
            var dogrulandi = p[3].Trim() switch
            {
                "dogrulandi" => true,
                "dogrulanmadi" => false,
                var d => throw new FormatException($"{kaynakAdi}:{no}: durum 'dogrulandi' ya da 'dogrulanmadi' olmalı: '{d}'."),
            };
            if (string.IsNullOrWhiteSpace(p[4])) throw new FormatException($"{kaynakAdi}:{no}: kaynak boş olamaz.");
            var kayit = new MeclisUyeSayisi(p[0].Trim(), yil, uye, dogrulandi, p[4].Trim());
            if (sonuc.Any(x => x.IlceKod == kayit.IlceKod && x.Yil == yil))
                throw new FormatException($"{kaynakAdi}:{no}: {kayit.IlceKod} {yil} iki kez yazılmış.");
            sonuc.Add(kayit);
        }
        return sonuc;
    }
}

/// <summary>Meclis hesaplayıcısı için bir ilçenin bir seçimdeki belediye meclisi listeleri ve oyları (sandık sonuçlarının toplamı).</summary>
public sealed class MeclisServisi(SecimDbContext db)
{
    /// <summary>
    /// Parti listeleri ve bağımsız adaylar, oyu çoktan aza. Geçerli oy = bu oyların toplamı (YSK sandık dosyalarında geçerli
    /// oy sütunu yok; K8). Yalnızca sandık düzeyindeki sonuçlar toplanır.
    /// </summary>
    public async Task<List<MeclisListesi>> ListelerAsync(int ilceId, int secimId, CancellationToken ct = default)
    {
        var satirlar = await db.Sonuclar.AsNoTracking()
            .Where(s => s.IlceId == ilceId && s.SecimId == secimId && s.OyTuru == OyTuru.BelediyeMeclisi && s.SandikId != null)
            .GroupBy(s => new
            {
                Parti = s.Parti != null ? s.Parti.KisaAd : s.Aday!.Parti != null ? s.Aday.Parti.KisaAd : null,
                Aday = s.Parti == null && s.Aday!.Parti == null ? s.Aday.AdSoyad : null,
            })
            .Select(g => new { g.Key.Parti, g.Key.Aday, Oy = g.Sum(s => (long)s.Oy) })
            .ToListAsync(ct);

        return satirlar
            .Select(x => x.Parti is { } p ? new MeclisListesi(p, x.Oy) : new MeclisListesi($"{x.Aday} (bağımsız)", x.Oy, Bagimsiz: true))
            .OrderByDescending(l => l.Oy).ThenBy(l => l.Ad, StringComparer.Ordinal)
            .ToList();
    }
}
