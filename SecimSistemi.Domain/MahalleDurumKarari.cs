namespace SecimSistemi.Domain;

public enum DurumKarariTuru
{
    /// <summary>Mahallenin (birimin) sınırı o seçimde değişmedi.</summary>
    Sabit,

    /// <summary>Mahallenin (birimin) sınırı o seçimde değişti; karşılaştırılabilir dönem burada başlar.</summary>
    SiniriDegisti,

    /// <summary>İki mahalle aynı karşılaştırma birimidir (bölünme, birleşme, ad ya da kod değişikliği).</summary>
    Esleme,
}

public enum KararDurumu
{
    Talep,
    Onaylandi,
    Reddedildi,
}

/// <summary>
/// Otomatik sınır tespitini düzelten insan kararı. Onaylanmış ve yerine başkası geçmemiş karar geçerlidir ve
/// otomatik tespitin önüne geçer. Mahalle eşlemelerinin tek kaynağı da bu tablodur.
/// Kararlar silinmez: aynı konuda yeni bir karar onaylanınca eskisinin <see cref="YerineGecenKararId"/> alanı dolar,
/// eski karar geçmiş olarak kalır (veritabanı silmeyi bir tetikleyiciyle reddeder).
/// <see cref="TalepEden"/> ve <see cref="Onaylayan"/> şimdilik serbest metindir; Sprint 7'de kullanıcı kimliğine bağlanacak.
/// </summary>
public class MahalleDurumKarari
{
    public int Id { get; private set; }

    /// <summary>Kararın konusu olan mahalle. Karar, bu mahallenin ait olduğu karşılaştırma birimine uygulanır.
    /// Eşlemede eski (önceki seçimlerde görünen) mahalledir.</summary>
    public int MahalleKoyId { get; private set; }
    public MahalleKoy MahalleKoy { get; private set; } = null!;

    public DurumKarariTuru Karar { get; private set; }

    /// <summary>Sabit ve SiniriDegisti kararlarında zorunlu: kararın geçerli olduğu seçim.</summary>
    public int? SecimId { get; private set; }
    public Secim? Secim { get; private set; }

    /// <summary>Yalnızca eşlemede: yeni (sonradan görünen) mahalle.</summary>
    public int? IliskiliMahalleKoyId { get; private set; }
    public MahalleKoy? IliskiliMahalleKoy { get; private set; }

    /// <summary>Yalnızca eşlemede.</summary>
    public EslemeTuru? EslemeTuru { get; private set; }

    /// <summary>Yalnızca eşlemede, isteğe bağlı: değişikliğin yürürlük tarihi. Bilinmiyorsa boş (uydurulmuş tarih yazılmaz).</summary>
    public DateOnly? YururlukTarihi { get; private set; }

    public string Gerekce { get; private set; } = "";

    /// <summary>Dayanak belge (örnek "Esenyurt Belediye Meclisi kararı 2017/45").</summary>
    public string? Kaynak { get; private set; }

    public string TalepEden { get; private set; } = "";
    public DateTimeOffset TalepZamani { get; private set; }

    public KararDurumu Durum { get; private set; }
    public string? Onaylayan { get; private set; }
    public DateTimeOffset? KararZamani { get; private set; }
    public string? KararNotu { get; private set; }

    /// <summary>Bu kararın yerine geçen (aynı konuda sonradan onaylanan) karar. Doluysa bu karar artık geçerli değildir.</summary>
    public int? YerineGecenKararId { get; private set; }
    public MahalleDurumKarari? YerineGecenKarar { get; private set; }

    public bool GecerliMi => Durum == KararDurumu.Onaylandi && YerineGecenKararId is null && YerineGecenKarar is null;

    private MahalleDurumKarari() { }

    public static MahalleDurumKarari DurumTalebi(MahalleKoy mahalle, Secim secim, DurumKarariTuru karar,
        string gerekce, string? kaynak, string talepEden, DateTimeOffset zaman)
    {
        ArgumentNullException.ThrowIfNull(mahalle);
        ArgumentNullException.ThrowIfNull(secim);
        if (karar == DurumKarariTuru.Esleme)
            throw new ArgumentException("Eşleme talebi için EslemeTalebi kullanılır.", nameof(karar));
        return new MahalleDurumKarari
        {
            MahalleKoy = mahalle, MahalleKoyId = mahalle.Id, Karar = karar, Secim = secim, SecimId = secim.Id,
            Gerekce = Zorunlu(gerekce, nameof(gerekce)), Kaynak = Bos(kaynak), TalepEden = Zorunlu(talepEden, nameof(talepEden)),
            TalepZamani = zaman.ToUniversalTime(), Durum = KararDurumu.Talep,
        };
    }

    public static MahalleDurumKarari EslemeTalebi(MahalleKoy eski, MahalleKoy yeni, EslemeTuru tur,
        string gerekce, string? kaynak, string talepEden, DateTimeOffset zaman, DateOnly? yururlukTarihi = null)
    {
        ArgumentNullException.ThrowIfNull(eski);
        ArgumentNullException.ThrowIfNull(yeni);
        if (ReferenceEquals(eski, yeni) || (eski.Id != 0 && eski.Id == yeni.Id))
            throw new ArgumentException("Bir mahalle kendisiyle eşlenemez.", nameof(yeni));
        if (eski.IlceId != yeni.IlceId)
            throw new ArgumentException($"'{eski.Ad}' ile '{yeni.Ad}' farklı ilçelerde; eşlenemez.", nameof(yeni));
        return new MahalleDurumKarari
        {
            MahalleKoy = eski, MahalleKoyId = eski.Id, Karar = DurumKarariTuru.Esleme,
            IliskiliMahalleKoy = yeni, IliskiliMahalleKoyId = yeni.Id, EslemeTuru = tur, YururlukTarihi = yururlukTarihi,
            Gerekce = Zorunlu(gerekce, nameof(gerekce)), Kaynak = Bos(kaynak), TalepEden = Zorunlu(talepEden, nameof(talepEden)),
            TalepZamani = zaman.ToUniversalTime(), Durum = KararDurumu.Talep,
        };
    }

    /// <summary>Talebi onaylar. Aynı konudaki önceki geçerli karar verilirse onun yerine geçilir.</summary>
    public void Onayla(string onaylayan, DateTimeOffset zaman, string? not, MahalleDurumKarari? oncekiGecerli)
    {
        TalepMi();
        if (oncekiGecerli is not null)
        {
            if (ReferenceEquals(oncekiGecerli, this)) throw new ArgumentException("Karar kendi yerine geçemez.", nameof(oncekiGecerli));
            if (!oncekiGecerli.GecerliMi) throw new InvalidOperationException($"{oncekiGecerli.Id} numaralı karar zaten geçerli değil.");
            if (!AyniKonu(oncekiGecerli)) throw new InvalidOperationException($"{oncekiGecerli.Id} numaralı karar başka bir konuda.");
            oncekiGecerli.YerineGecenKarar = this;
        }
        Durum = KararDurumu.Onaylandi;
        Onaylayan = Zorunlu(onaylayan, nameof(onaylayan));
        KararZamani = zaman.ToUniversalTime();
        KararNotu = Bos(not);
    }

    public void Reddet(string karariVeren, DateTimeOffset zaman, string? not)
    {
        TalepMi();
        Durum = KararDurumu.Reddedildi;
        Onaylayan = Zorunlu(karariVeren, nameof(karariVeren));
        KararZamani = zaman.ToUniversalTime();
        KararNotu = Bos(not);
    }

    /// <summary>
    /// İki karar aynı konuda mı: durum kararlarında aynı mahalle ve seçim (Sabit ile SiniriDegisti birbirinin yerine geçebilir);
    /// eşlemede yönden bağımsız aynı mahalle çifti.
    /// </summary>
    public bool AyniKonu(MahalleDurumKarari diger)
    {
        bool esleme = Karar == DurumKarariTuru.Esleme, digerEsleme = diger.Karar == DurumKarariTuru.Esleme;
        if (esleme != digerEsleme) return false;
        if (!esleme) return MahalleKoyId == diger.MahalleKoyId && SecimId == diger.SecimId;
        return (MahalleKoyId == diger.MahalleKoyId && IliskiliMahalleKoyId == diger.IliskiliMahalleKoyId)
               || (MahalleKoyId == diger.IliskiliMahalleKoyId && IliskiliMahalleKoyId == diger.MahalleKoyId);
    }

    private void TalepMi()
    {
        if (Durum != KararDurumu.Talep)
            throw new InvalidOperationException($"{Id} numaralı karar '{Durum}' durumunda; yalnızca talepler onaylanır ya da reddedilir.");
    }

    private static string Zorunlu(string deger, string ad) =>
        string.IsNullOrWhiteSpace(deger) ? throw new ArgumentException("Boş olamaz.", ad) : deger.Trim();

    private static string? Bos(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
