namespace SecimSistemi.Domain;

/// <summary>
/// Eski bir mahalle/köyün yenisine bağlantısı (birleşme, bölünme, ad ya da kod değişikliği).
/// Bölünmede bir eski birim birden çok yeni birime, birleşmede birden çok eski birim tek yeni birime bağlanır.
/// </summary>
public class MahalleKoyEslemesi
{
    public int Id { get; set; }

    public int EskiMahalleKoyId { get; set; }
    public MahalleKoy EskiMahalleKoy { get; set; } = null!;

    public int YeniMahalleKoyId { get; set; }
    public MahalleKoy YeniMahalleKoy { get; set; } = null!;

    /// <summary>Değişikliğin yürürlüğe girdiği tarih. Kesin tarih bilinmiyorsa boştur (uydurulmuş tarih yazılmaz).</summary>
    public DateOnly? GecerlilikTarihi { get; set; }

    public required EslemeTuru Tur { get; set; }

    public string? Not { get; set; }
}

/// <summary>Eski bir ilçenin yenisine bağlantısı (örnek: 2008'de Büyükçekmece'den ayrılan Esenyurt).</summary>
public class IlceEslemesi
{
    public int Id { get; set; }

    public int EskiIlceId { get; set; }
    public Ilce EskiIlce { get; set; } = null!;

    public int YeniIlceId { get; set; }
    public Ilce YeniIlce { get; set; } = null!;

    public required DateOnly GecerlilikTarihi { get; set; }

    public required EslemeTuru Tur { get; set; }

    public string? Not { get; set; }
}
