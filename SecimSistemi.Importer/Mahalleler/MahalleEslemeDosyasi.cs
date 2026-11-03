using System.Globalization;
using System.Text;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Mahalleler;

public enum EslemeDurumu
{
    /// <summary>Araç tarafından önerildi; veritabanına gitmez.</summary>
    Oneri,

    /// <summary>Kullanıcı onayladı; veritabanına gider ve birimleri belirler.</summary>
    Onayli,

    /// <summary>Kullanıcı reddetti; yeniden önerilmez.</summary>
    Ret,
}

public sealed record MahalleEslemeSatiri(
    int EskiKod, string EskiAd, int YeniKod, string YeniAd,
    EslemeTuru? Tur, DateOnly? Tarih, EslemeDurumu Durum, double? Benzerlik, string Not)
{
    /// <summary>Yönden bağımsız çift: (A, B) ile (B, A) aynı eşlemedir.</summary>
    public (int, int) Cift => EskiKod < YeniKod ? (EskiKod, YeniKod) : (YeniKod, EskiKod);
}

/// <summary>
/// <c>Tanimlar/mahalle-eslemeleri.csv</c>: mahalle/köy eşlemeleri. Araç öneri satırları ekler; yalnızca kullanıcının
/// "onayli" yaptığı satırlar veritabanına gider. Dosya her yazılışta baştan üretilir: satır aralarındaki yorumlar
/// korunmaz, açıklama için "not" sütunu kullanılır.
/// </summary>
public static class MahalleEslemeDosyasi
{
    public const string BaslikSatiri = "eski_kod;eski_ad;yeni_kod;yeni_ad;tur;tarih;durum;benzerlik;not";

    private static readonly Dictionary<string, EslemeTuru> Turler = new()
    {
        ["birlesme"] = EslemeTuru.Birlesme,
        ["bolunme"] = EslemeTuru.Bolunme,
        ["ad-degisikligi"] = EslemeTuru.AdDegisikligi,
        ["kod-degisikligi"] = EslemeTuru.KodDegisikligi,
    };

    private static readonly Dictionary<string, EslemeDurumu> Durumlar = new()
    {
        ["oneri"] = EslemeDurumu.Oneri,
        ["onayli"] = EslemeDurumu.Onayli,
        ["ret"] = EslemeDurumu.Ret,
    };

    private const string Aciklama = """
        # Mahalle/köy eşlemeleri. Yalnızca durum = onayli olan satırlar veritabanına gider (komut: mahalle esleme-yukle <ilçe kodu>).
        # Onaylı eşlemeyle birbirine bağlanan mahalleler tek bir "karşılaştırma birimi" olur; yıllar arası karşılaştırma bu birimde yapılır.
        #
        # Sütunlar (noktalı virgülle ayrılır):
        #   eski_kod, yeni_kod  YSK Muhtarlık Id. eski = önceki seçimlerde görünen, yeni = sonradan görünen birim.
        #   eski_ad, yeni_ad    Bilgi için; doğrulamada kullanılmaz.
        #   tur                 birlesme | bolunme | ad-degisikligi | kod-degisikligi (onayli satırda zorunlu)
        #   tarih               Değişikliğin yürürlük tarihi, yyyy-aa-gg. Bilinmiyorsa boş bırakın.
        #   durum               oneri (araç önerdi) | onayli (siz onayladınız) | ret (siz reddettiniz; yeniden önerilmez)
        #   benzerlik           Ad benzerliği 0–1 (yalnızca bilgi)
        #   not                 Serbest açıklama. Noktalı virgül kullanmayın.
        #
        # Elle satır ekleyebilirsiniz (örnek bölünme): 119319;ARDIÇLI MAH.;147450;YENİ MAH.;bolunme;;onayli;;Resmî Gazete ...
        # Satır aralarına yazılan yorumlar dosya yeniden yazılınca kaybolur; açıklamayı "not" sütununa yazın.
        """;

    public static string VarsayilanYol => Veritabani.DepoYolu("SecimSistemi.Importer", "Tanimlar", "mahalle-eslemeleri.csv");

    public static IReadOnlyList<MahalleEslemeSatiri> Yukle(string? yol = null)
    {
        yol ??= VarsayilanYol;
        if (!File.Exists(yol)) return [];
        using var okuyucu = new StreamReader(yol, Encoding.UTF8);
        return Oku(okuyucu, Path.GetFileName(yol));
    }

    public static IReadOnlyList<MahalleEslemeSatiri> Oku(TextReader okuyucu, string kaynak)
    {
        var hatalar = new List<string>();
        var satirlar = new List<(int No, MahalleEslemeSatiri Satir)>();
        bool baslikGoruldu = false;
        int no = 0;

        for (var satir = okuyucu.ReadLine(); satir is not null; satir = okuyucu.ReadLine())
        {
            no++;
            if (string.IsNullOrWhiteSpace(satir) || satir.TrimStart().StartsWith('#')) continue;
            if (!baslikGoruldu)
            {
                if (satir.Trim() != BaslikSatiri)
                    throw new YuklemeHatasi($"{kaynak}: {no}. satır '{BaslikSatiri}' olmalı.");
                baslikGoruldu = true;
                continue;
            }

            var a = satir.Split(';').Select(x => x.Trim()).ToArray();
            if (a.Length != 9)
            {
                hatalar.Add($"{no}. satır: 9 alan bekleniyordu, {a.Length} var");
                continue;
            }

            int Kod(string deger, string ad)
            {
                if (int.TryParse(deger, NumberStyles.None, CultureInfo.InvariantCulture, out var k) && k > 0) return k;
                hatalar.Add($"{no}. satır: {ad} '{deger}' pozitif bir tam sayı değil");
                return 0;
            }
            var eski = Kod(a[0], "eski_kod");
            var yeni = Kod(a[2], "yeni_kod");
            if (eski != 0 && eski == yeni) hatalar.Add($"{no}. satır: eski_kod ile yeni_kod aynı");

            EslemeTuru? tur = null;
            if (a[4].Length > 0)
            {
                if (Turler.TryGetValue(a[4], out var t)) tur = t;
                else hatalar.Add($"{no}. satır: tur '{a[4]}' olamaz ({string.Join(", ", Turler.Keys)})");
            }

            DateOnly? tarih = null;
            if (a[5].Length > 0)
            {
                if (DateOnly.TryParseExact(a[5], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) tarih = t;
                else hatalar.Add($"{no}. satır: tarih '{a[5]}' yyyy-aa-gg biçiminde değil");
            }

            if (!Durumlar.TryGetValue(a[6], out var durum))
            {
                hatalar.Add($"{no}. satır: durum '{a[6]}' olamaz ({string.Join(", ", Durumlar.Keys)})");
                continue;
            }
            if (durum == EslemeDurumu.Onayli && tur is null)
                hatalar.Add($"{no}. satır: onayli satırda tur dolu olmalı");

            double? benzerlik = null;
            if (a[7].Length > 0)
            {
                if (double.TryParse(a[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) benzerlik = b;
                else hatalar.Add($"{no}. satır: benzerlik '{a[7]}' sayı değil (ondalık ayracı nokta)");
            }

            satirlar.Add((no, new MahalleEslemeSatiri(eski, a[1], yeni, a[3], tur, tarih, durum, benzerlik, a[8])));
        }

        if (!baslikGoruldu)
            throw new YuklemeHatasi($"{kaynak}: başlık satırı yok.");

        foreach (var g in satirlar.GroupBy(s => s.Satir.Cift).Where(g => g.Count() > 1))
            hatalar.Add($"{string.Join(", ", g.Select(s => s.No))}. satırlar: {g.Key.Item1} ile {g.Key.Item2} arasındaki eşleme birden çok kez yazılmış");

        if (hatalar.Count > 0)
            throw new YuklemeHatasi($"{kaynak}: {hatalar.Count} hata var:{Environment.NewLine}{string.Join(Environment.NewLine, hatalar.Select(h => "  " + h))}");
        return satirlar.Select(s => s.Satir).ToList();
    }

    public static void Yaz(TextWriter yazici, IEnumerable<MahalleEslemeSatiri> satirlar)
    {
        yazici.WriteLine(Aciklama);
        yazici.WriteLine();
        yazici.WriteLine(BaslikSatiri);
        foreach (var s in satirlar)
        {
            var tur = s.Tur is { } t ? Turler.First(x => x.Value == t).Key : "";
            var durum = Durumlar.First(x => x.Value == s.Durum).Key;
            var alanlar = new[]
            {
                s.EskiKod.ToString(CultureInfo.InvariantCulture), s.EskiAd, s.YeniKod.ToString(CultureInfo.InvariantCulture), s.YeniAd,
                tur, s.Tarih?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", durum,
                s.Benzerlik?.ToString("0.00", CultureInfo.InvariantCulture) ?? "", s.Not,
            };
            yazici.WriteLine(string.Join(';', alanlar.Select(x => x.Replace(';', ','))));
        }
    }

    public static void Kaydet(string yol, IEnumerable<MahalleEslemeSatiri> satirlar)
    {
        var gecici = yol + ".yaziliyor";
        using (var yazici = new StreamWriter(gecici, false, new UTF8Encoding(false)))
            Yaz(yazici, satirlar);
        File.Move(gecici, yol, overwrite: true);
    }

    /// <summary>
    /// Önerileri mevcut satırlara ekler. Aynı çift (yönden bağımsız) zaten varsa (oneri, onayli ya da ret) eklenmez;
    /// mevcut satırlara hiç dokunulmaz.
    /// </summary>
    public static (IReadOnlyList<MahalleEslemeSatiri> Satirlar, int Eklenen) OnerileriEkle(
        IReadOnlyList<MahalleEslemeSatiri> mevcut, IEnumerable<MahalleEslemeSatiri> oneriler)
    {
        var sonuc = mevcut.ToList();
        var ciftler = mevcut.Select(s => s.Cift).ToHashSet();
        int eklenen = 0;
        foreach (var o in oneriler)
        {
            if (!ciftler.Add(o.Cift)) continue;
            sonuc.Add(o with { Durum = EslemeDurumu.Oneri });
            eklenen++;
        }
        return (sonuc, eklenen);
    }
}
