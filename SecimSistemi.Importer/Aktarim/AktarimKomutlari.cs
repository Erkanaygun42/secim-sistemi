using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SecimSistemi.Data;
using SecimSistemi.Domain;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Aktarim;

/// <summary><c>yukle</c> ve <c>yukle-hepsi</c> komutları.</summary>
internal static class AktarimKomutlari
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public static async Task<int> YukleHepsiAsync(string klasor, bool yenile)
    {
        var dosyalar = Directory.GetFiles(klasor, "*.xlsx")
            .Order(StringComparer.Ordinal)
            .Where(f => !DosyaKimligi.Coz(f).KontrolDosyasiMi)
            .ToList();
        if (dosyalar.Count == 0)
            throw new YuklemeHatasi($"{klasor} klasöründe sandık dosyası yok.");
        return await YukleAsync(dosyalar, yenile);
    }

    /// <summary>Dosyaları sırayla yükler; bir dosyada hata olursa orada durur (önceki dosyalar yüklü kalır).</summary>
    public static async Task<int> YukleAsync(IReadOnlyList<string> dosyalar, bool yenile)
    {
        var secenekler = await Veritabani.HazirlaAsync();

        var tanimlar = BaslikTanimlari.Yukle();
        int sira = 0;
        foreach (var yol in dosyalar)
        {
            sira++;
            // Arşiv: data/raw/x.xlsx → data/arsiv/<sha256>.xlsx
            var arsiv = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(yol))!, "..", "arsiv"));
            var sure = Stopwatch.StartNew();
            await using var db = new SecimDbContext(secenekler);
            var ozet = await new Yukleyici(db, tanimlar, arsiv).YukleAsync(yol, yenile);
            Yazdir(sira, dosyalar.Count, ozet, sure.Elapsed);
        }
        return 0;
    }

    private static void Yazdir(int sira, int toplam, YuklemeOzeti o, TimeSpan sure)
    {
        var islem = o.Islem switch
        {
            YuklemeIslemi.Yuklendi => "YÜKLENDİ",
            YuklemeIslemi.Yenilendi => "YENİLENDİ",
            _ => "ATLANDI",
        };
        var kontrol = o.Kontrol switch
        {
            KontrolSonucu.DosyaYok => "kontrol dosyası yok",
            KontrolSonucu.Yapilmadi => "yapılmadı",
            KontrolSonucu.Tutarli => "tutarlı",
            _ => "FARK VAR",
        };
        Console.WriteLine($"[{sira}/{toplam}] {o.DosyaAdi}: {islem} ({sure.TotalSeconds.ToString("0.0", Turkce)} sn)");
        Console.WriteLine(string.Create(Turkce,
            $"  satır {o.SatirSayisi:N0} · sandık {o.SandikSayisi:N0} · mahalle {o.MahalleSayisi:N0} · toplam oy {o.ToplamOy:N0} · DSP {(o.DspOyu is { } d ? d.ToString("N0", Turkce) : "sütun yok")} · kontrol: {kontrol}"));
        foreach (var u in o.Uyarilar)
            Console.WriteLine($"  uyarı: {u}");
    }
}
