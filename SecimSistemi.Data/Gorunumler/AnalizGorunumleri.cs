using SecimSistemi.Domain;

namespace SecimSistemi.Data.Gorunumler;

// PostgreSQL görünümlerinin (migration: AnalizGorunumleri) anahtarsız karşılıkları. Yalnızca okunur.
// Oranlar yüzde (0–100). DSP pusulada değilse (Girmedi, başka partinin listesi ya da kayıt yok)
// DSP oyu ve oranları 0 değil boştur.

/// <summary>"MahalleKarnesi": seçim × oy türü × karşılaştırma birimi başına toplamlar.</summary>
public sealed class MahalleKarnesiSatiri
{
    public int SecimId { get; init; }
    public DateOnly Tarih { get; init; }
    public int Yil { get; init; }
    public SecimTuru SecimTuru { get; init; }
    public OyTuru OyTuru { get; init; }
    public int IlceId { get; init; }
    public int BirimId { get; init; }
    public int BirimKod { get; init; }
    public string BirimAdi { get; init; } = "";
    public int SandikSayisi { get; init; }
    public long ToplamOy { get; init; }
    public bool PusuladaMi { get; init; }
    public long? DspOyu { get; init; }
    public decimal? DspOrani { get; init; }
}

/// <summary>"SecimDegisimi": ardışık iki yerel seçim arasında birim ve oy türü başına değişim.</summary>
public sealed class SecimDegisimiSatiri
{
    public int IlceId { get; init; }
    public int BirimId { get; init; }
    public int BirimKod { get; init; }
    public string BirimAdi { get; init; } = "";
    public OyTuru OyTuru { get; init; }
    public int OncekiSecimId { get; init; }
    public int OncekiYil { get; init; }
    public int SecimId { get; init; }
    public int Yil { get; init; }
    public long? OncekiToplamOy { get; init; }
    public long ToplamOy { get; init; }
    public decimal? OncekiOran { get; init; }
    public decimal? Oran { get; init; }
    /// <summary>Oran − önceki oran, yüzde puan.</summary>
    public decimal? PuanFarki { get; init; }
    public long? OncekiDspOyu { get; init; }
    public long? DspOyu { get; init; }
    public long? OyFarki { get; init; }
}

/// <summary>"AdayEtkisi": aynı seçimde başkanlık oranı − belediye meclisi oranı.</summary>
public sealed class AdayEtkisiSatiri
{
    public int SecimId { get; init; }
    public DateOnly Tarih { get; init; }
    public int Yil { get; init; }
    public int IlceId { get; init; }
    public int BirimId { get; init; }
    public int BirimKod { get; init; }
    public string BirimAdi { get; init; } = "";
    /// <summary>BelediyeBaskani ya da BuyuksehirBaskani.</summary>
    public OyTuru BaskanlikTuru { get; init; }
    public decimal? MeclisOrani { get; init; }
    public decimal? BaskanlikOrani { get; init; }
    /// <summary>Başkanlık oranı − meclis oranı, yüzde puan.</summary>
    public decimal? AdayEtkisiPuan { get; init; }
    public long? MeclisOyu { get; init; }
    public long? BaskanlikOyu { get; init; }
    /// <summary>Başkanlık oyu − meclis oyu.</summary>
    public long? OyFarki { get; init; }
}
