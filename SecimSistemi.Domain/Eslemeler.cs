namespace SecimSistemi.Domain;

// Mahalle/köy eşlemeleri MahalleDurumKarari tablosunda tutulur (Karar = Esleme); tek kaynak orasıdır.

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
