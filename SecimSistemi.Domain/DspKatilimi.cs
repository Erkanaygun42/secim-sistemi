namespace SecimSistemi.Domain;

/// <summary>
/// DSP'nin bir seçimde, bir oy türünde, bir bölgedeki durumu. <see cref="IlceId"/> boşsa kayıt il geneli içindir.
/// Örnek: 2024 Esenyurt belediye meclisi = KendiListesi, IttifakAdi = "Cumhur İttifakı".
/// </summary>
public class DspKatilimi
{
    public int Id { get; private set; }

    public int SecimId { get; private set; }
    public Secim Secim { get; private set; } = null!;

    public OyTuru OyTuru { get; private set; }

    public int IlId { get; private set; }
    public Il Il { get; private set; } = null!;

    public int? IlceId { get; private set; }
    public Ilce? Ilce { get; private set; }

    public DspDurumu Durum { get; private set; }

    /// <summary>Yalnızca <see cref="DspDurumu.BaskaPartininListesi"/> için dolu: DSP adaylarının girdiği listenin partisi.</summary>
    public int? ListePartiId { get; private set; }
    public Parti? ListePartisi { get; private set; }

    /// <summary>DSP'nin içinde yer aldığı ittifak (örnek "Cumhur İttifakı"); yoksa boş.</summary>
    public string? IttifakAdi { get; private set; }

    public string? Not { get; set; }

    private DspKatilimi() { }

    public static DspKatilimi Olustur(Secim secim, OyTuru oyTuru, Il il, Ilce? ilce, DspDurumu durum,
        Parti? listePartisi = null, string? ittifakAdi = null, string? not = null)
    {
        ArgumentNullException.ThrowIfNull(secim);
        ArgumentNullException.ThrowIfNull(il);

        if (durum == DspDurumu.BaskaPartininListesi && listePartisi is null)
            throw new ArgumentException("Başka partinin listesinde girildiyse liste partisi belirtilmeli.", nameof(listePartisi));
        if (durum != DspDurumu.BaskaPartininListesi && listePartisi is not null)
            throw new ArgumentException($"Liste partisi yalnızca {nameof(DspDurumu.BaskaPartininListesi)} durumunda belirtilir.", nameof(listePartisi));
        if (ilce is not null && !ReferenceEquals(ilce.Il, il) && !(il.Id != 0 && ilce.IlId == il.Id))
            throw new ArgumentException($"'{ilce.Ad}' ilçesi '{il.Ad}' iline ait değil.", nameof(ilce));

        return new DspKatilimi
        {
            Secim = secim,
            SecimId = secim.Id,
            OyTuru = oyTuru,
            Il = il,
            IlId = il.Id,
            Ilce = ilce,
            IlceId = ilce?.Id,
            Durum = durum,
            ListePartisi = listePartisi,
            ListePartiId = listePartisi?.Id,
            IttifakAdi = string.IsNullOrWhiteSpace(ittifakAdi) ? null : ittifakAdi.Trim(),
            Not = not,
        };
    }
}
