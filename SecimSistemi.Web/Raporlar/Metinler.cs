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
    public static string Puan(decimal? v) => v is null ? Bos : (v.Value > 0 ? "+" : v.Value < 0 ? "−" : "") + Math.Abs(v.Value).ToString("N2", Turkce);

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
