using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Okuma;

/// <summary>Denetimde beklenen bir dosya ve bulunup bulunmadığı.</summary>
public sealed record BeklenenDosya(string Ad, IlceTanimi Ilce, int Yil, OyTuru OyTuru, bool Var);

/// <summary>
/// Klasör denetiminin sonucu. <see cref="Hatalar"/> doluysa yükleme başlamaz; <see cref="Eksikler"/> ve
/// <see cref="Uyarilar"/> yalnızca bildirilir (yükleme mevcut dosyalarla devam eder).
/// </summary>
public sealed record DenetimSonucu(string Klasor, IReadOnlyList<BeklenenDosya> Beklenenler, IReadOnlyList<string> Hatalar,
    IReadOnlyList<string> Uyarilar)
{
    public IReadOnlyList<BeklenenDosya> Eksikler => Beklenenler.Where(b => !b.Var).ToList();
    public bool Temiz => Hatalar.Count == 0;
}

/// <summary>
/// <c>data/raw</c> klasörünün indirme denetimi (veritabanına dokunmaz; docs/kararlar.md K43):
/// <list type="bullet">
/// <item><b>Kontrol listesi:</b> tanımlı her ilçe (<see cref="BolgeTanimlari.Ilceler"/>) × her yerel seçim yılı × ilin oy türleri
///   (İstanbul büyükşehir: belediye meclisi, belediye başkanı, büyükşehir başkanı). Eksikler uyarıdır.</item>
/// <item><b>Hata:</b> xlsx olmayan dosya; adı düzene uymayan dosya (doğru ad tahmin edilebiliyorsa önerilir); açılamayan dosya;
///   içeriği aynı iki dosya (aynı ilçe iki kez indirilmiş); aynı yılda başka bir ilçenin mahalle kimliklerini içeren dosya
///   (ilçe adı yanlış).</item>
/// <item><b>Uyarı:</b> aynı ilçe ve yılın dosyalarında sandık sayısı farklı.</item>
/// </list>
/// Dosyada il, ilçe ya da oy türü bilgisi yoktur (docs/ysk-dosya-yapisi.md); bu yüzden içerikten ancak dolaylı çelişkiler yakalanır.
/// </summary>
public static class DosyaDenetimi
{
    /// <summary>İlin büyükşehir olup olmamasına göre beklenen oy türleri. İstanbul ve Kocaeli büyükşehirdir (il genel meclisi yok).</summary>
    public static IReadOnlyList<OyTuru> OyTurleri(IlTanimi il) => [OyTuru.BelediyeMeclisi, OyTuru.BelediyeBaskani, OyTuru.BuyuksehirBaskani];

    public static string DosyaAdi(int yil, OyTuru oyTuru, IlceTanimi ilce) => $"{yil}_yerel_{OyTuruAdlari.Ad(oyTuru)}_{ilce.DosyaAdi}.xlsx";

    /// <summary>Kontrol listesi: tanımlı bütün ilçeler, yıllar ve oy türleri.</summary>
    public static IReadOnlyList<(string Ad, IlceTanimi Ilce, int Yil, OyTuru OyTuru)> KontrolListesi() =>
        [.. from ilce in BolgeTanimlari.Ilceler.Values
            from yil in DosyaKimligi.YerelSecimTarihleri.Keys.Order()
            from tur in OyTurleri(ilce.Il)
            select (DosyaAdi(yil, tur, ilce), ilce, yil, tur)];

    public static DenetimSonucu Denetle(string klasor)
    {
        if (!Directory.Exists(klasor)) throw new YuklemeHatasi($"{klasor} klasörü yok.");
        var hatalar = new List<string>();
        var uyarilar = new List<string>();
        var okunan = new List<(DosyaKimligi Kimlik, YskSandikDosyasi Dosya, string Ozet)>();

        foreach (var yol in Directory.GetFiles(klasor).Order(StringComparer.Ordinal))
        {
            var ad = Path.GetFileName(yol);
            if (ad.StartsWith("~$", StringComparison.Ordinal))
            {
                uyarilar.Add($"{ad}: Excel'in kilit dosyası (dosya Excel'de açık); yok sayıldı.");
                continue;
            }
            if (!ad.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                hatalar.Add($"{ad}: xlsx dosyası değil. Portaldan \"Excel İndir\" ile alınan .xlsx dosyası gerekir.");
                continue;
            }

            DosyaKimligi kimlik;
            try
            {
                kimlik = DosyaKimligi.Coz(yol);
            }
            catch (YuklemeHatasi h)
            {
                hatalar.Add(Oner(ad) is { } dogru && dogru != ad ? $"{h.Message} Doğru ad büyük olasılıkla: {dogru}" : h.Message);
                continue;
            }
            if (kimlik.KontrolDosyasiMi) continue;

            try
            {
                var dosya = YskDosyaOkuyucu.Oku(yol);
                okunan.Add((kimlik, dosya, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(yol)))));
            }
            catch (Exception e) when (e is YuklemeHatasi or IOException or InvalidDataException or FormatException or ArgumentException)
            {
                hatalar.Add($"{ad}: dosya okunamadı ({e.Message}).");
            }
        }

        // İçeriği aynı iki dosya: aynı ilçe/oy türü iki kez indirilip farklı adla kaydedilmiş.
        foreach (var g in okunan.GroupBy(o => o.Ozet).Where(g => g.Count() > 1))
            hatalar.Add($"İçeriği aynı dosyalar: {string.Join(", ", g.Select(o => o.Kimlik.DosyaAdi))}. Biri yanlış indirilmiş ya da yanlış adlandırılmış.");

        // Aynı yılda iki ilçenin mahalle kimlikleri kesişiyorsa dosyalardan biri yanlış ilçe adıyla kaydedilmiştir.
        foreach (var yil in okunan.GroupBy(o => o.Kimlik.Yil))
        {
            var ilceler = yil.GroupBy(o => o.Kimlik.IlceAdi)
                .Select(g => (Ilce: g.Key, Mahalleler: g.SelectMany(o => o.Dosya.Satirlar.Select(s => s.MuhtarlikId)).ToHashSet(), Dosyalar: g.ToList()))
                .ToList();
            for (var i = 0; i < ilceler.Count; i++)
                for (var j = i + 1; j < ilceler.Count; j++)
                {
                    var ortak = ilceler[i].Mahalleler.Intersect(ilceler[j].Mahalleler).Count();
                    if (ortak > 0)
                        hatalar.Add($"{yil.Key}: '{ilceler[i].Ilce}' ve '{ilceler[j].Ilce}' dosyalarında {ortak} ortak mahalle kimliği var; " +
                                    "dosyalardan biri yanlış ilçe adıyla kaydedilmiş olabilir.");
                }

            // Aynı ilçe ve yılın üç dosyası aynı sandıkları içermeli (Esenyurt'ta öyle; docs/ysk-dosya-yapisi.md).
            foreach (var (ilce, _, dosyalar) in ilceler)
            {
                var sayilar = dosyalar.Select(o => (o.Kimlik.DosyaAdi, Sandik: o.Dosya.Satirlar.Count)).ToList();
                if (sayilar.Select(x => x.Sandik).Distinct().Count() > 1)
                    uyarilar.Add($"{yil.Key} {ilce}: sandık sayıları farklı ({string.Join(", ", sayilar.Select(x => $"{x.DosyaAdi}: {x.Sandik}"))}). " +
                                 "Dosyalardan biri başka bir ilçeye ya da yıla ait olabilir.");
            }
        }

        var bulunan = okunan.Select(o => o.Kimlik.DosyaAdi).ToHashSet(StringComparer.Ordinal);
        var beklenen = KontrolListesi().Select(k => new BeklenenDosya(k.Ad, k.Ilce, k.Yil, k.OyTuru, bulunan.Contains(k.Ad))).ToList();
        return new DenetimSonucu(klasor, beklenen, hatalar, uyarilar);
    }

    /// <summary>
    /// Düzene uymayan addan doğru adı tahmin eder: küçük harf, Türkçe harfler ASCII, boşluk ve alt çizgiler düzene göre.
    /// Örnek: "2024_Yerel_Belediye-Meclisi_Şişli.xlsx" → "2024_yerel_belediye-meclisi_sisli.xlsx". Tahmin edilemezse boş.
    /// </summary>
    public static string? Oner(string ad)
    {
        var govde = Path.GetFileNameWithoutExtension(ad).ToLower(new CultureInfo("tr-TR"));
        var ascii = new StringBuilder();
        foreach (var c in govde)
            ascii.Append(c switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u', ' ' => '-', _ => c });
        var parcalar = ascii.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parcalar.Length != 4) return null;
        var aday = $"{parcalar[0]}_{parcalar[1]}_{parcalar[2].Trim('-')}_{parcalar[3].Trim('-')}.xlsx";
        try
        {
            DosyaKimligi.Coz(aday);
            return aday;
        }
        catch (YuklemeHatasi)
        {
            return null;
        }
    }

    public static void Yazdir(DenetimSonucu s, TextWriter cikti)
    {
        var yillar = DosyaKimligi.YerelSecimTarihleri.Keys.Order().ToList();
        var turler = s.Beklenenler.Select(b => b.OyTuru).Distinct().ToList();
        cikti.WriteLine($"Dosya denetimi: {s.Klasor}");
        cikti.WriteLine($"Kontrol listesi: {s.Beklenenler.Count} dosya ({s.Beklenenler.Select(b => b.Ilce).Distinct().Count()} ilçe × {yillar.Count} yıl × " +
                        $"{turler.Count} oy türü: {string.Join(", ", turler.Select(OyTuruAdlari.Ad))}); ✓ var, · eksik");
        foreach (var ilce in s.Beklenenler.GroupBy(b => b.Ilce))
            cikti.WriteLine($"  {ilce.Key.Ad,-12} " + string.Join("  ", yillar.Select(y =>
                $"{y} {string.Concat(turler.Select(t => ilce.Single(b => b.Yil == y && b.OyTuru == t).Var ? "✓" : "·"))}")));

        if (s.Eksikler.Count > 0)
        {
            cikti.WriteLine($"Eksik ({s.Eksikler.Count}):");
            foreach (var e in s.Eksikler) cikti.WriteLine($"  {e.Ad}");
        }
        foreach (var u in s.Uyarilar) cikti.WriteLine($"UYARI: {u}");
        foreach (var h in s.Hatalar) cikti.WriteLine($"HATA: {h}");
        cikti.WriteLine(s.Temiz
            ? $"Sonuç: hata yok; {s.Beklenenler.Count - s.Eksikler.Count}/{s.Beklenenler.Count} dosya var."
            : $"Sonuç: {s.Hatalar.Count} hata. Dosyaları düzeltip denetimi yeniden çalıştırın.");
    }
}
