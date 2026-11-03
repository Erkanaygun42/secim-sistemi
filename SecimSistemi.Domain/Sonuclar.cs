namespace SecimSistemi.Domain;

/// <summary>
/// Bir seçimin, bir oy türünün, bir bölgedeki kaydı. <see cref="Sonuc"/> ve <see cref="OyOzeti"/> bu yapıyı paylaşır.
/// Bölge sütunları <see cref="Bolge"/> üzerinden doldurulur: seçilen düzey ve bütün üst düzeyler dolu, alt düzeyler boş.
/// Bu sınıf tek başına bir tablo değildir; alanları türeyen sınıfların tablolarına yazılır.
/// </summary>
public abstract class BolgeKaydi
{
    public int Id { get; private set; }

    public int SecimId { get; private set; }
    public Secim Secim { get; private set; } = null!;

    public OyTuru OyTuru { get; private set; }
    public BolgeDuzeyi Duzey { get; private set; }

    public int IlId { get; private set; }
    public Il Il { get; private set; } = null!;

    public int? IlceId { get; private set; }
    public Ilce? Ilce { get; private set; }

    public int? MahalleKoyId { get; private set; }
    public MahalleKoy? MahalleKoy { get; private set; }

    public int? SandikId { get; private set; }
    public Sandik? Sandik { get; private set; }

    /// <summary>EF Core veritabanından okurken kullanır.</summary>
    protected BolgeKaydi() { }

    protected BolgeKaydi(Secim secim, OyTuru oyTuru, Bolge bolge)
    {
        ArgumentNullException.ThrowIfNull(secim);
        ArgumentNullException.ThrowIfNull(bolge);

        if (bolge.Sandik is { } sandik && !AyniSecim(sandik, secim))
            throw new ArgumentException($"{sandik.No} numaralı sandık '{secim.Ad}' seçimine ait değil.", nameof(bolge));

        Secim = secim;
        SecimId = secim.Id;
        OyTuru = oyTuru;
        Duzey = bolge.Duzey;

        Il = bolge.Il;
        IlId = bolge.Il.Id;
        Ilce = bolge.Ilce;
        IlceId = bolge.Ilce?.Id;
        MahalleKoy = bolge.MahalleKoy;
        MahalleKoyId = bolge.MahalleKoy?.Id;
        Sandik = bolge.Sandik;
        SandikId = bolge.Sandik?.Id;
    }

    private static bool AyniSecim(Sandik sandik, Secim secim) =>
        ReferenceEquals(sandik.Secim, secim) || (secim.Id != 0 && sandik.SecimId == secim.Id);
}

/// <summary>
/// Uzun biçimde tek bir oy sayısı: bölge, seçim, oy türü, parti ya da bağımsız aday, oy.
/// Yeni parti ya da yeni seçim tablo yapısını değiştirmez.
/// </summary>
public sealed class Sonuc : BolgeKaydi
{
    public int? PartiId { get; private set; }
    public Parti? Parti { get; private set; }

    public int? AdayId { get; private set; }
    public Aday? Aday { get; private set; }

    public int Oy { get; private set; }

    private Sonuc() { }

    private Sonuc(Secim secim, OyTuru oyTuru, Bolge bolge, int oy) : base(secim, oyTuru, bolge)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(oy);
        Oy = oy;
    }

    public static Sonuc PartiOyu(Secim secim, OyTuru oyTuru, Bolge bolge, Parti parti, int oy)
    {
        ArgumentNullException.ThrowIfNull(parti);
        return new Sonuc(secim, oyTuru, bolge, oy) { Parti = parti, PartiId = parti.Id };
    }

    public static Sonuc AdayOyu(Secim secim, OyTuru oyTuru, Bolge bolge, Aday aday, int oy)
    {
        ArgumentNullException.ThrowIfNull(aday);
        if (aday.OyTuru != oyTuru)
            throw new ArgumentException($"'{aday.AdSoyad}' {aday.OyTuru} adayı; {oyTuru} sonucu olarak kaydedilemez.", nameof(aday));
        if (!ReferenceEquals(aday.Secim, secim) && !(secim.Id != 0 && aday.SecimId == secim.Id))
            throw new ArgumentException($"'{aday.AdSoyad}' '{secim.Ad}' seçiminin adayı değil.", nameof(aday));

        return new Sonuc(secim, oyTuru, bolge, oy) { Aday = aday, AdayId = aday.Id };
    }
}

/// <summary>
/// Bir bölgedeki seçmen ve oy toplamları. YSK pilot dosyalarında bu bilgiler olmadığı için dört alan da boş olabilir.
/// Geçerli oy ayrıca bölgedeki <see cref="Sonuc"/> oylarının toplamından hesaplanabilir; ilan edilen değer burada tutulur.
/// </summary>
public sealed class OyOzeti : BolgeKaydi
{
    public int? KayitliSecmen { get; private set; }
    public int? OyKullanan { get; private set; }
    public int? GecerliOy { get; private set; }
    public int? GecersizOy { get; private set; }

    private OyOzeti() { }

    private OyOzeti(Secim secim, OyTuru oyTuru, Bolge bolge) : base(secim, oyTuru, bolge) { }

    public static OyOzeti Olustur(Secim secim, OyTuru oyTuru, Bolge bolge,
        int? kayitliSecmen = null, int? oyKullanan = null, int? gecerliOy = null, int? gecersizOy = null)
    {
        foreach (var (deger, ad) in new[] { (kayitliSecmen, nameof(kayitliSecmen)), (oyKullanan, nameof(oyKullanan)), (gecerliOy, nameof(gecerliOy)), (gecersizOy, nameof(gecersizOy)) })
        {
            if (deger < 0) throw new ArgumentOutOfRangeException(ad, deger, "Negatif olamaz.");
        }

        return new OyOzeti(secim, oyTuru, bolge)
        {
            KayitliSecmen = kayitliSecmen,
            OyKullanan = oyKullanan,
            GecerliOy = gecerliOy,
            GecersizOy = gecersizOy,
        };
    }
}
