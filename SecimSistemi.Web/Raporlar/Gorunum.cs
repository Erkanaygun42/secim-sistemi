using System.Globalization;
using System.Text.RegularExpressions;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Görünüm ayarları. Renkler tek yerde durur: <c>appsettings.json</c> →
/// <c>"Gorunum": { "AnaRenk": "#16c0ed", "IkinciRenk": "#f04248" }</c>. Varsayılanlar DSP logosunun mavisi ve kırmızısıdır.
/// Tonu değiştirmek için yalnızca bu ayarı değiştirip uygulamayı yeniden başlatın.
/// <list type="bullet">
/// <item><see cref="AnaRenk"/>: üst çubuğun zemini, "partiye özel" bandı, PDF başlığının çizgisi, grafiklerin ilk serisi.</item>
/// <item><see cref="IkinciRenk"/>: logo kırmızısı. Şimdilik hiçbir yerde kullanılmaz; uyarı renkleriyle (sarı, kırmızı) karışmasın.</item>
/// <item><see cref="Koyu"/>: aynı tonun koyulaştırılmış hali (beyaz üzerinde en az 5,5:1 kontrast); butonlar, bağlantılar,
///   yazılar. Logo mavisi açık olduğu için yazıda doğrudan kullanılamaz; koyu ton ana renkten hesaplanır, ayrıca ayarlanmaz.</item>
/// </list>
/// </summary>
public sealed partial record Gorunum
{
    /// <summary>DSP logosundaki mavi (web sürümünden ölçüldü).</summary>
    public const string VarsayilanRenk = "#16c0ed";

    /// <summary>DSP logosundaki kırmızı.</summary>
    public const string VarsayilanIkinciRenk = "#f04248";

    /// <summary>
    /// Ana renk zemin üzerindeki yazı ve simge rengi: koyu lacivert. Logo mavisi üzerinde beyaz 2,1:1 kontrastla okunmaz,
    /// lacivert 7,4:1 ile okunur.
    /// </summary>
    public const string BantYazisi = "#0b2530";

    public static readonly Gorunum Varsayilan = new(VarsayilanRenk);

    public Gorunum(string anaRenk, string ikinciRenk = VarsayilanIkinciRenk)
    {
        AnaRenk = Dogrula("Gorunum:AnaRenk", anaRenk, VarsayilanRenk);
        IkinciRenk = Dogrula("Gorunum:IkinciRenk", ikinciRenk, VarsayilanIkinciRenk);
        Koyu = Koyulastir(AnaRenk, 5.5);
    }

    private static string Dogrula(string ad, string renk, string ornek) =>
        RenkKalibi().IsMatch(renk) ? renk.ToLowerInvariant()
            : throw new InvalidOperationException($"'{ad}' ayarı #rrggbb biçiminde olmalı (örnek {ornek}); verilen: '{renk}'.");

    public string AnaRenk { get; }

    /// <summary>Logo kırmızısı; şimdilik kullanılmıyor (uyarı renkleriyle karışmasın diye).</summary>
    public string IkinciRenk { get; }

    public string Koyu { get; }

    /// <summary>
    /// Grafik serilerinin renkleri; sıra sabittir ve renk seriyi izler, sırasını değil (meclis her zaman 1. renk).
    /// 1. renk ana renktir; diğerleri (turuncu, mor, sarı) logo mavisiyle birlikte renk körlüğü ve beyaz zemin için denetlendi.
    /// Açık renkler beyaz zeminde düşük kontrastlıdır; bu yüzden grafiklerde değer etiketi ve altında tablo her zaman vardır.
    /// </summary>
    public IReadOnlyList<string> SeriRenkleri => [AnaRenk, "#f47230", "#6a4ce2", "#f2a600"];

    public string SeriRengi(int sira) => SeriRenkleri[Math.Clamp(sira, 0, SeriRenkleri.Count - 1)];

    public static Gorunum Oku(IConfiguration ayarlar) =>
        new(ayarlar["Gorunum:AnaRenk"] ?? VarsayilanRenk, ayarlar["Gorunum:IkinciRenk"] ?? VarsayilanIkinciRenk);

    /// <summary>Rengin tonunu koruyup (HSL) açıklığını, beyaz üzerindeki kontrast <paramref name="kontrast"/> olana kadar azaltır.</summary>
    public static string Koyulastir(string renk, double kontrast)
    {
        var (r, g, b) = Coz(renk);
        var (h, s, l) = Hsl(r, g, b);
        for (var x = l; x > 0; x -= 0.005)
        {
            var (r2, g2, b2) = Rgb(h, s, x);
            if (BeyazaKontrast(r2, g2, b2) >= kontrast) return Yaz(r2, g2, b2);
        }
        return "#000000";
    }

    public static double BeyazaKontrast(string renk)
    {
        var (r, g, b) = Coz(renk);
        return BeyazaKontrast(r, g, b);
    }

    private static double BeyazaKontrast(double r, double g, double b)
    {
        static double Dogrusal(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var parlaklik = 0.2126 * Dogrusal(r) + 0.7152 * Dogrusal(g) + 0.0722 * Dogrusal(b);
        return 1.05 / (parlaklik + 0.05);
    }

    private static (double, double, double) Coz(string renk) =>
        (int.Parse(renk[1..3], NumberStyles.HexNumber) / 255.0,
         int.Parse(renk[3..5], NumberStyles.HexNumber) / 255.0,
         int.Parse(renk[5..7], NumberStyles.HexNumber) / 255.0);

    private static string Yaz(double r, double g, double b) =>
        $"#{(int)Math.Round(r * 255):x2}{(int)Math.Round(g * 255):x2}{(int)Math.Round(b * 255):x2}";

    private static (double H, double S, double L) Hsl(double r, double g, double b)
    {
        double enCok = Math.Max(r, Math.Max(g, b)), enAz = Math.Min(r, Math.Min(g, b)), l = (enCok + enAz) / 2;
        if (enCok == enAz) return (0, 0, l);
        var d = enCok - enAz;
        var s = l > 0.5 ? d / (2 - enCok - enAz) : d / (enCok + enAz);
        var h = enCok == r ? (g - b) / d + (g < b ? 6 : 0) : enCok == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static (double, double, double) Rgb(double h, double s, double l)
    {
        static double Ton(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            return t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p;
        }
        if (s == 0) return (l, l, l);
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return (Ton(p, q, h + 1 / 3.0), Ton(p, q, h), Ton(p, q, h - 1 / 3.0));
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex RenkKalibi();
}
