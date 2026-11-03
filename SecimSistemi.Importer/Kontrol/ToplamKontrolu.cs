using System.Globalization;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Kontrol;

public enum KalemTuru
{
    Parti,
    Aday,
}

/// <summary>Karşılaştırılan bir oy kalemi: bir parti ya da bağımsız aday. Ad normalleştirilmiş tutulur.</summary>
public readonly record struct Kalem(KalemTuru Tur, string Ad)
{
    public static Kalem Parti(string kisaAd) => new(KalemTuru.Parti, PartiAdEslemesi.Normallestir(kisaAd));
    public static Kalem Aday(string adSoyad) => new(KalemTuru.Aday, PartiAdEslemesi.Normallestir(adSoyad));

    public override string ToString() => Tur == KalemTuru.Parti ? Ad : $"{Ad} (bağımsız)";
}

/// <summary>
/// Bir kalemde sandıklardan hesaplanan toplam ile ilan edilen toplamın farkı.
/// Değer boşsa kalem o tarafta hiç yoktur.
/// </summary>
public sealed record KalemFarki(Kalem Kalem, long? Hesaplanan, long? Ilan)
{
    public long Fark => (Hesaplanan ?? 0) - (Ilan ?? 0);
}

public sealed record KontrolRaporu(
    IReadOnlyList<KalemFarki> Farklar,
    IReadOnlyList<Kalem> TekTaraftaSifir,
    long HesaplananToplam,
    long? IlanGecerliOy)
{
    /// <summary>İlan edilen geçerli oy yoksa bu karşılaştırma yapılmaz ve tutarlı sayılır.</summary>
    public bool GecerliOyTutuyor => IlanGecerliOy is null || IlanGecerliOy == HesaplananToplam;

    public bool TutarliMi => Farklar.Count == 0 && GecerliOyTutuyor;
}

/// <summary>
/// Sandıklardan hesaplanan parti/aday toplamlarını, YSK'nın ilçe tablosunda ilan ettiği oylarla karşılaştırır.
/// Bir kalem yalnızca bir tarafta varsa ve oyu sıfırdan büyükse fark sayılır. İki tarafta da sıfır
/// sayılabiliyorsa (yalnızca bir tarafta, oyu 0) fark değil, bilgi olarak <see cref="KontrolRaporu.TekTaraftaSifir"/>'a yazılır.
/// </summary>
public static class ToplamKontrolu
{
    private static readonly StringComparer Turkce = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false);

    public static KontrolRaporu Karsilastir(
        IReadOnlyDictionary<Kalem, long> hesaplanan,
        IReadOnlyDictionary<Kalem, long> ilan,
        long? ilanGecerliOy)
    {
        ArgumentNullException.ThrowIfNull(hesaplanan);
        ArgumentNullException.ThrowIfNull(ilan);

        var farklar = new List<KalemFarki>();
        var tekTaraftaSifir = new List<Kalem>();

        foreach (var kalem in hesaplanan.Keys.Union(ilan.Keys).OrderBy(k => k.Tur).ThenBy(k => k.Ad, Turkce))
        {
            long? h = hesaplanan.TryGetValue(kalem, out var hv) ? hv : null;
            long? i = ilan.TryGetValue(kalem, out var iv) ? iv : null;

            if ((h ?? 0) != (i ?? 0))
                farklar.Add(new KalemFarki(kalem, h, i));
            else if (h is null || i is null)
                tekTaraftaSifir.Add(kalem);
        }

        return new KontrolRaporu(farklar, tekTaraftaSifir, hesaplanan.Values.Sum(), ilanGecerliOy);
    }
}
