using System.Globalization;
using System.Text.RegularExpressions;

namespace SecimSistemi.Domain;

public class Parti
{
    public int Id { get; set; }

    /// <summary>Uygulamada gösterilen kısa ad (örnek: "DSP", "İYİ PARTİ").</summary>
    public required string KisaAd { get; set; }

    public string? TamAd { get; set; }
}

/// <summary>
/// YSK dosyalarındaki bir sütun başlığını bir partiye bağlar.
/// Aynı yazım farklı dönemlerde farklı partiyi gösterebilir: 2014'teki "İP" İşçi Partisi'dir,
/// İYİ Parti değil. Bu yüzden eşleme bir tarih aralığında geçerlidir; iki uç da boş olabilir.
/// </summary>
public partial class PartiAdEslemesi
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public int Id { get; set; }

    /// <summary>Normalleştirilmiş başlık: <see cref="Normallestir"/> ile üretilir.</summary>
    public required string HamAd { get; set; }

    public int PartiId { get; set; }
    public Parti Parti { get; set; } = null!;

    /// <summary>Bu tarihten (dahil) itibaren geçerli. Boşsa başlangıç sınırı yok.</summary>
    public DateOnly? GecerliBaslangic { get; set; }

    /// <summary>Bu tarihe (dahil) kadar geçerli. Boşsa bitiş sınırı yok.</summary>
    public DateOnly? GecerliBitis { get; set; }

    public bool GecerliMi(DateOnly tarih) =>
        (GecerliBaslangic is null || tarih >= GecerliBaslangic) &&
        (GecerliBitis is null || tarih <= GecerliBitis);

    /// <summary>
    /// Başlığı karşılaştırılabilir biçime getirir: baştaki/sondaki boşluklar silinir, aradaki boşluklar teke iner,
    /// Türkçe kurallarla büyük harfe çevrilir ("DEM Parti" → "DEM PARTİ").
    /// </summary>
    public static string Normallestir(string baslik)
    {
        ArgumentNullException.ThrowIfNull(baslik);
        return CokluBosluk().Replace(baslik.Trim(), " ").ToUpper(Turkce);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CokluBosluk();
}

/// <summary>
/// Bir seçimde bir oy türünde yarışan aday. YSK dosyalarında parti adaylarının oyu parti sütununda geldiği için
/// şimdilik yalnızca bağımsızlar kaydedilir; <see cref="PartiId"/> boşsa aday bağımsızdır.
/// Adayın yarıştığı bölge: büyükşehir başkanı için il, belediye başkanı ve meclis için ilçe.
/// </summary>
public class Aday
{
    public int Id { get; set; }
    public required string AdSoyad { get; set; }

    public int SecimId { get; set; }
    public Secim Secim { get; set; } = null!;

    public required OyTuru OyTuru { get; set; }

    public int IlId { get; set; }
    public Il Il { get; set; } = null!;

    public int? IlceId { get; set; }
    public Ilce? Ilce { get; set; }

    public int? PartiId { get; set; }
    public Parti? Parti { get; set; }

    public bool BagimsizMi => PartiId is null && Parti is null;
}
