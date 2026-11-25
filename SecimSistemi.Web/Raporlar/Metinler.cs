using System.Globalization;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>Sayfalarda, Excel'de ve PDF'te ortak metinler ve sayı biçimleri.</summary>
public static class Metinler
{
    public static readonly CultureInfo Turkce = new("tr-TR");

    public const string Bos = "—";
    public const string PusuladaDegil = "DSP pusulada değil";
    public const string VeriKaynagi = "YSK Açık Veri Portalı, sandık düzeyi sonuçlar";
    public const string PartiyeOzel = "PARTİYE ÖZEL — dağıtılmaz";

    // Adreste kullanılan oy türü kodları (Importer'daki dosya adı yazımıyla aynı).
    private static readonly Dictionary<string, OyTuru> OyTuruKodlari = new()
    {
        ["belediye-meclisi"] = OyTuru.BelediyeMeclisi,
        ["il-genel-meclisi"] = OyTuru.IlGenelMeclisi,
        ["belediye-baskani"] = OyTuru.BelediyeBaskani,
        ["buyuksehir-baskani"] = OyTuru.BuyuksehirBaskani,
        ["milletvekili"] = OyTuru.Milletvekili,
        ["cumhurbaskani"] = OyTuru.Cumhurbaskani,
    };

    public static string Kod(OyTuru o) => OyTuruKodlari.First(x => x.Value == o).Key;

    public static OyTuru? OyTuruCoz(string? kod) => kod is not null && OyTuruKodlari.TryGetValue(kod, out var o) ? o : null;

    public static string Ad(OyTuru o) => o switch
    {
        OyTuru.BelediyeMeclisi => "Belediye meclisi",
        OyTuru.IlGenelMeclisi => "İl genel meclisi",
        OyTuru.BelediyeBaskani => "Belediye başkanı",
        OyTuru.BuyuksehirBaskani => "Büyükşehir başkanı",
        OyTuru.Milletvekili => "Milletvekili",
        OyTuru.Cumhurbaskani => "Cumhurbaşkanı",
        _ => o.ToString(),
    };

    public static string Ad(SinirDurumu d) => d switch
    {
        SinirDurumu.SonradanKuruldu => "Sonradan kuruldu",
        SinirDurumu.Yeni => "Yeni",
        SinirDurumu.Sabit => "Sabit",
        SinirDurumu.Supheli => "Şüpheli",
        SinirDurumu.SiniriDegisti => "Sınırı değişti",
        _ => d.ToString(),
    };

    /// <summary>"Sabit", "Şüpheli" ... İnsan kararıyla gelen durumun sonuna " ✓" eklenir.</summary>
    public static string Sinir(BirimYilDurumu? d) => d is null ? Bos : Ad(d.Sonuc) + (d.Dogrulandi ? " ✓" : "");

    /// <summary>Durumun açıklaması: pay değişimi ve varsa insan kararı.</summary>
    public static string SinirAciklamasi(BirimYilDurumu d)
    {
        var parcalar = new List<string>();
        if (d.PayDegisimi is { } p) parcalar.Add($"{d.Yil} seçiminde ilçe meclis oyundaki pay {Yuzde(p)} değişti");
        if (d.Dogrulandi) parcalar.Add($"insan kararıyla doğrulandı ({d.Karar})");
        else if (d.Otomatik != d.Sonuc) parcalar.Add($"otomatik tespit: {Ad(d.Otomatik)}");
        return parcalar.Count == 0 ? Ad(d.Sonuc) : string.Join("; ", parcalar);
    }

    /// <summary>
    /// Dönemin sınır özeti: "Şüpheli (2019)", "Sınırı değişti (2019) ✓", "Yeni (2019)", "Sabit". Dönem içinde şüphe varsa
    /// hiçbir zaman "Sabit" yazmaz.
    /// </summary>
    public static string Sinir(DonemSiniri? d) => d is null ? Bos
        : Ad(d.Durum) + (d.Yillar.Count > 0 && d.Durum != SinirDurumu.SonradanKuruldu ? $" ({string.Join(", ", d.Yillar)})" : "")
          + (d.Dogrulandi ? " ✓" : "");

    /// <summary>Dönem sınır özetinin açıklaması (üzerine gelince görünür).</summary>
    public static string SinirAciklamasi(DonemSiniri d) => d.Durum switch
    {
        SinirDurumu.Supheli => Uyari(d.Ilgili),
        SinirDurumu.SiniriDegisti =>
            $"Sınır {d.DonemBaslangici} seçiminde değişti" + Ek(d.Ilgili[0]) + $". Karşılaştırılabilir dönem {d.Donem}; {d.DonemBaslangici} öncesiyle karşılaştırılamaz.",
        SinirDurumu.Yeni =>
            $"Mahalle ilk kez {d.DonemBaslangici} seçiminde görünüyor (o seçimde kuruldu ya da bölünerek ayrıldı). Karşılaştırılabilir dönem {d.Donem}.",
        SinirDurumu.SonradanKuruldu => $"Mahalle {d.Yil} seçiminde henüz yoktu.",
        _ => $"Karşılaştırılabilir dönem ({d.Donem}) içinde sınır değişikliği bulunmadı" +
             (d.Dogrulandi ? $"; {string.Join(", ", d.Ilgili.Select(x => x.Yil))} seçimi insan kararıyla doğrulandı ({d.Ilgili[0].Karar})." : "."),
    };

    private static string Ek(BirimYilDurumu y) =>
        (y.PayDegisimi is { } p ? $" (ilçe meclis oyundaki pay {Yuzde(p)})" : "") + (y.Dogrulandi ? $", insan kararıyla doğrulandı ({y.Karar})" : "");

    /// <summary>Yılın yönelme eki, okunuşa göre: 2009'a, 2014'e, 2002'ye, 2006'ya, 2010'a, 2020'ye.</summary>
    public static string Yonelme(int yil)
    {
        var ek = (yil % 10) switch
        {
            1 or 3 or 4 or 5 or 8 => "e",
            2 or 7 => "ye",
            6 => "ya",
            9 => "a",
            _ => ((yil / 10) % 10) switch { 1 or 3 or 4 or 6 or 9 => "a", 2 or 5 => "ye", _ => "e" },
        };
        return $"{yil}'{ek}";
    }

    public static string Ad(MahalleGrubu g) => g switch
    {
        MahalleGrubu.Kale => "Kale",
        MahalleGrubu.Erozyon => "Erozyon",
        MahalleGrubu.Yukselen => "Yükselen",
        MahalleGrubu.Zayif => "Zayıf",
        _ => g.ToString(),
    };

    /// <summary>Grubun tek satırlık tanımı (sayfada ve dosyada).</summary>
    public static string Tanim(MahalleGrubu g) => g switch
    {
        MahalleGrubu.Kale => "dönem başında güçlüydü, korudu ya da artırdı",
        MahalleGrubu.Erozyon => "dönem başında güçlüydü, kaybetti",
        MahalleGrubu.Yukselen => "dönem başında zayıftı, kazandı",
        MahalleGrubu.Zayif => "dönem başında zayıftı, kazanamadı",
        _ => "",
    };

    public static string Ad(Egilim e) => e switch
    {
        Egilim.Kaybetti => "Kaybetti",
        Egilim.Degismedi => "Değişmedi",
        Egilim.Kazandi => "Kazandı",
        _ => e.ToString(),
    };

    /// <summary>Göreli değişim (0,30 = %30), işaretli: +%30 / −%92.</summary>
    public static string Goreli(decimal? v) => v is null ? Bos : Yuzde(v.Value);

    /// <summary>Yılın bulunma eki, okunuşa göre: 2009'da, 2014'te, 2002'de, 2006'da, 2010'da, 2040'ta.</summary>
    public static string Bulunma(int yil)
    {
        // Okunuştaki son sözcük: birler basamağı, o 0 ise onlar, o da 0 ise "bin" / "yüz".
        var son = (yil % 10) != 0 ? (yil % 10) switch
            {
                1 => "bir", 2 => "iki", 3 => "üç", 4 => "dört", 5 => "beş", 6 => "altı", 7 => "yedi", 8 => "sekiz", _ => "dokuz",
            }
            : ((yil / 10) % 10) != 0 ? ((yil / 10) % 10) switch
            {
                1 => "on", 2 => "yirmi", 3 => "otuz", 4 => "kırk", 5 => "elli", 6 => "altmış", 7 => "yetmiş", 8 => "seksen", _ => "doksan",
            }
            : ((yil / 100) % 10) != 0 ? "yüz" : "bin";
        var sert = "çfhkpsşt".Contains(son[^1]);
        var kalin = "aıou".Contains(son.Last(c => "aeıioöuü".Contains(c)));
        return $"{yil}'{(sert ? 't' : 'd')}{(kalin ? 'a' : 'e')}";
    }

    /// <summary>Grafikte oy türünün renk sırası; renk seriyi izler (meclis her zaman 1. renk, ana renk).</summary>
    public static int SeriSirasi(OyTuru o) => o switch
    {
        OyTuru.BelediyeMeclisi => 0,
        OyTuru.BelediyeBaskani => 1,
        OyTuru.BuyuksehirBaskani => 2,
        _ => 3,
    };

    /// <summary>Şüpheli uyarısı: "2019 seçiminde pay +%48 değişti (şüpheli), sınır kararı girilmedi."</summary>
    public static string Uyari(IEnumerable<BirimYilDurumu> supheliler) =>
        string.Join(" ", supheliler.Select(d =>
            $"{d.Yil} seçiminde ilçe meclis oyundaki pay {(d.PayDegisimi is { } p ? Yuzde(p) : "belirgin")} değişti (şüpheli), sınır kararı girilmedi."))
        + " Yıllar arası karşılaştırmada dikkatli olun.";

    /// <summary>Oy sayısı: 12.345; boşsa "—".</summary>
    public static string Oy(long? v) => v is null ? Bos : v.Value.ToString("N0", Turkce);

    public static string Oy(decimal? v) => v is null ? Bos : v.Value.ToString("N0", Turkce);

    /// <summary>Oran yüzde (0–100) olarak tutulur: 0,98; boşsa "—".</summary>
    public static string Oran(decimal? v) => v is null ? Bos : v.Value.ToString("N2", Turkce);

    /// <summary>Yüzde puan farkı, işaretli: +0,16 / −0,41.</summary>
    public static string Puan(decimal? v)
    {
        if (v is null) return Bos;
        var r = Math.Round(v.Value, 2, MidpointRounding.AwayFromZero); // −0,004 "−0,00" değil "0,00" yazılsın
        return (r > 0 ? "+" : r < 0 ? "−" : "") + Math.Abs(r).ToString("N2", Turkce);
    }

    /// <summary>Oran (0–1) yüzde olarak, işaretli: +%48 / −%21.</summary>
    public static string Yuzde(decimal v) => (v > 0 ? "+" : v < 0 ? "−" : "") + "%" + Math.Abs(v * 100).ToString("N0", Turkce);

    public static string Katsayi(decimal? v) => v is null ? Bos : v.Value.ToString("N2", Turkce);

    public static string Esikler(SinirEsikleri e) =>
        $"Sınır eşikleri: düşüş %{e.Dusus * 100:0}, artış %{e.Artis * 100:0}, şüpheli %{e.Supheli * 100:0} " +
        "(ilçe meclis oyundaki pay değişimi; yalnızca ilçede yeni mahalle çıkan seçimlerde).";

    public const string EslemeNotu =
        "Mahalle eşlemeleri henüz onaylanmadı: her mahalle ayrı karşılaştırma birimidir. Bölünen mahallelerde karşılaştırılabilir " +
        "dönem sınırın değiştiği seçimde başlar (docs/kararlar.md K6, K14).";

    /// <summary>Türkiye saatiyle şimdi.</summary>
    public static DateTime TurkiyeSaati(DateTimeOffset an) =>
        TimeZoneInfo.ConvertTime(an, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime;
}
