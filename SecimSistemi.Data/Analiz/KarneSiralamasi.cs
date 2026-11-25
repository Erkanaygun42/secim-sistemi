using System.Globalization;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Data.Analiz;

public static class Siralama
{
    /// <summary>Türkçe alfabe sırası (Ç, Ğ, İ, Ö, Ş, Ü doğru yerde).</summary>
    public static readonly StringComparer Turkce = StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true);
}

public enum KarneSiraAlani { Kod, Ad, Sandik, ToplamOy, DspOyu, DspOrani, PuanFarki, Sinir }

/// <summary>
/// Karne satırlarının sıralaması. Ekranda, Excel'de ve PDF'te aynı sıra kullanılır.
/// Boş değerler (DSP pusulada değil, sınır durumu yok) yönden bağımsız olarak her zaman en sonda kalır.
/// Eşitlikte birim adı (Türkçe alfabe), sonra kod belirler.
/// </summary>
public static class KarneSiralamasi
{
    public static List<KarneSatiri> Uygula(IEnumerable<KarneSatiri> satirlar, KarneSiraAlani alan, bool azalan)
    {
        var liste = satirlar.ToList();
        var dolu = liste.Where(s => Anahtar(s, alan) is not null);
        var bos = liste.Where(s => Anahtar(s, alan) is null).OrderBy(s => s.BirimAdi, Siralama.Turkce).ThenBy(s => s.BirimKod);

        IOrderedEnumerable<KarneSatiri> sirali = alan == KarneSiraAlani.Ad
            ? (azalan ? dolu.OrderByDescending(s => s.BirimAdi, Siralama.Turkce) : dolu.OrderBy(s => s.BirimAdi, Siralama.Turkce))
            : (azalan ? dolu.OrderByDescending(s => (IComparable)Anahtar(s, alan)!) : dolu.OrderBy(s => (IComparable)Anahtar(s, alan)!));

        return [.. sirali.ThenBy(s => s.BirimAdi, Siralama.Turkce).ThenBy(s => s.BirimKod), .. bos];
    }

    private static object? Anahtar(KarneSatiri s, KarneSiraAlani alan) => alan switch
    {
        KarneSiraAlani.Kod => s.BirimKod,
        KarneSiraAlani.Ad => s.BirimAdi,
        KarneSiraAlani.Sandik => s.SandikSayisi,
        KarneSiraAlani.ToplamOy => s.ToplamOy,
        KarneSiraAlani.DspOyu => s.DspOyu,
        KarneSiraAlani.DspOrani => s.DspOrani,
        KarneSiraAlani.PuanFarki => s.PuanFarki,
        KarneSiraAlani.Sinir => s.DonemSiniri is null ? null : SinirOnceligi(s.DonemSiniri),
        _ => throw new ArgumentOutOfRangeException(nameof(alan), alan, null),
    };

    /// <summary>
    /// Sınır sütununda artan sıra: önce dikkat isteyenler, dönemin sınır özetine göre (yalnızca o yıl değil):
    /// dönemi şüpheli, sınırı değişmiş, yeni; sonra sabit.
    /// </summary>
    private static int SinirOnceligi(DonemSiniri d) => d.Durum switch
    {
        SinirDurumu.Supheli => 0,
        SinirDurumu.SiniriDegisti => 1,
        SinirDurumu.Yeni => 2,
        SinirDurumu.Sabit => 3,
        _ => 4,
    };
}
