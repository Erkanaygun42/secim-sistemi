namespace SecimSistemi.Domain;

/// <summary>
/// Yıllar arası karşılaştırmanın yapıldığı birim: onaylı eşlemelerle birbirine bağlı mahalle/köylerin tamamı.
/// Eşlemesi olmayan mahalle tek başına bir birimdir. <see cref="Kod"/> birimdeki en küçük Muhtarlık Id'dir;
/// birimler yeniden kurulsa da değişmez. Birimler eşlemelerden üretilir, elle düzenlenmez.
/// </summary>
public class KarsilastirmaBirimi
{
    public int Id { get; set; }

    public int IlceId { get; set; }
    public Ilce Ilce { get; set; } = null!;

    public required int Kod { get; set; }

    /// <summary>Üye mahallelerin adları, Türkçe alfabe sırasıyla " + " ile birleştirilmiş.</summary>
    public required string Ad { get; set; }

    public List<KarsilastirmaBirimiUyesi> Uyeler { get; set; } = [];
}

/// <summary>Bir mahalle/köyün ait olduğu karşılaştırma birimi. Her mahalle tam olarak bir birime aittir.</summary>
public class KarsilastirmaBirimiUyesi
{
    public int MahalleKoyId { get; set; }
    public MahalleKoy MahalleKoy { get; set; } = null!;

    public int BirimId { get; set; }
    public KarsilastirmaBirimi Birim { get; set; } = null!;
}
