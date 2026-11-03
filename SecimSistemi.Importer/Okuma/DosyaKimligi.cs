using System.Text.RegularExpressions;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Okuma;

public sealed record IlTanimi(int Kod, string Ad);

/// <summary>Dosya adındaki ilçe (örnek "esenyurt") ve veritabanındaki karşılığı.</summary>
public sealed record IlceTanimi(string DosyaAdi, string Kod, string Ad, IlTanimi Il);

/// <summary>Dosya adlarından tanınan bölgeler. Yeni ilçe yüklenecekse buraya eklenir.</summary>
public static class BolgeTanimlari
{
    public static readonly IlTanimi Istanbul = new(34, "İSTANBUL");

    /// <summary>
    /// İlçe kodu şimdilik geçicidir: "plaka-AD". Resmî ilçe koduna geçiş ileride eşleme/migration ile yapılacak.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IlceTanimi> Ilceler = new Dictionary<string, IlceTanimi>
    {
        ["esenyurt"] = new("esenyurt", "34-ESENYURT", "ESENYURT", Istanbul),
    };
}

/// <summary>Oy türlerinin dosya adlarındaki yazımı.</summary>
public static class OyTuruAdlari
{
    private static readonly Dictionary<string, OyTuru> Adlar = new()
    {
        ["belediye-meclisi"] = OyTuru.BelediyeMeclisi,
        ["il-genel-meclisi"] = OyTuru.IlGenelMeclisi,
        ["belediye-baskani"] = OyTuru.BelediyeBaskani,
        ["buyuksehir-baskani"] = OyTuru.BuyuksehirBaskani,
    };

    public static IEnumerable<string> Hepsi => Adlar.Keys;

    public static bool TryCoz(string ad, out OyTuru oyTuru) => Adlar.TryGetValue(ad, out oyTuru);

    public static string Ad(OyTuru oyTuru) =>
        Adlar.FirstOrDefault(x => x.Value == oyTuru).Key
        ?? throw new ArgumentOutOfRangeException(nameof(oyTuru), oyTuru, "Dosya adında karşılığı yok.");
}

/// <summary>
/// Dosya adından çıkan bilgiler. Düzen: <c>yil_yerel_oy-turu_ilce.xlsx</c>
/// (örnek <c>2024_yerel_belediye-meclisi_esenyurt.xlsx</c>). İlçe yerinde <c>istanbul-ilceler</c> yazan dosya
/// sandık dosyası değil, ilçe toplamlarını içeren kontrol dosyasıdır.
/// </summary>
public sealed partial record DosyaKimligi(string DosyaAdi, DateOnly SecimTarihi, OyTuru OyTuru, string IlceAdi)
{
    public const string KontrolDosyasiIlceAdi = "istanbul-ilceler";

    /// <summary>YSK Açık Veri Portalı'ndaki yerel seçimler. Yeni yıl yüklenecekse buraya eklenir.</summary>
    public static readonly IReadOnlyDictionary<int, DateOnly> YerelSecimTarihleri = new Dictionary<int, DateOnly>
    {
        [2009] = new(2009, 3, 29),
        [2014] = new(2014, 3, 30),
        [2019] = new(2019, 3, 31),
        [2024] = new(2024, 3, 31),
    };

    public int Yil => SecimTarihi.Year;

    public bool KontrolDosyasiMi => IlceAdi == KontrolDosyasiIlceAdi;

    /// <summary>Sandık dosyasının ilçesi. Kontrol dosyasında ilçe yoktur.</summary>
    public IlceTanimi Ilce => KontrolDosyasiMi
        ? throw new InvalidOperationException($"{DosyaAdi} bir kontrol dosyası; tek bir ilçeye ait değil.")
        : BolgeTanimlari.Ilceler[IlceAdi];

    public static DosyaKimligi Coz(string yol)
    {
        var ad = Path.GetFileName(yol);
        var m = DosyaAdiDuzeni().Match(ad);
        if (!m.Success)
            throw new YuklemeHatasi($"'{ad}' dosya adı düzene uymuyor. Beklenen: yil_yerel_oy-turu_ilce.xlsx (örnek 2024_yerel_belediye-meclisi_esenyurt.xlsx).");

        var yil = int.Parse(m.Groups["yil"].Value);
        if (!YerelSecimTarihleri.TryGetValue(yil, out var tarih))
            throw new YuklemeHatasi($"'{ad}': {yil} yılında tanımlı yerel seçim yok. Tanımlı yıllar: {string.Join(", ", YerelSecimTarihleri.Keys)}.");

        var oyTuruAdi = m.Groups["oyturu"].Value;
        if (!OyTuruAdlari.TryCoz(oyTuruAdi, out var oyTuru))
            throw new YuklemeHatasi($"'{ad}': '{oyTuruAdi}' tanımlı bir oy türü değil. Tanımlılar: {string.Join(", ", OyTuruAdlari.Hepsi)}.");

        var ilce = m.Groups["ilce"].Value;
        if (ilce != KontrolDosyasiIlceAdi && !BolgeTanimlari.Ilceler.ContainsKey(ilce))
            throw new YuklemeHatasi($"'{ad}': '{ilce}' tanımlı bir ilçe değil. Tanımlılar: {string.Join(", ", BolgeTanimlari.Ilceler.Keys)}.");

        return new DosyaKimligi(ad, tarih, oyTuru, ilce);
    }

    [GeneratedRegex(@"^(?<yil>\d{4})_yerel_(?<oyturu>[a-z-]+)_(?<ilce>[a-z0-9-]+)\.xlsx$")]
    private static partial Regex DosyaAdiDuzeni();
}
