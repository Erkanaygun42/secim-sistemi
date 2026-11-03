namespace SecimSistemi.Domain;

public enum YuklemeDurumu
{
    /// <summary>Sonuçları veritabanında duran yükleme. Bir seçim + oy türü + ilçe için en fazla bir tane olur.</summary>
    Aktif,

    /// <summary>Yerine başka bir dosya yüklendi (--yenile); sonuçları silindi, kaydı geçmiş için duruyor.</summary>
    Yenilendi,
}

public enum KontrolSonucu
{
    /// <summary>YSK ilçe tablosu (kontrol dosyası) bulunamadı.</summary>
    DosyaYok,

    /// <summary>Kontrol dosyası var ama okunamadı ya da kontrol yapılmadı.</summary>
    Yapilmadi,

    Tutarli,
    FarkVar,
}

/// <summary>
/// Bir YSK dosyasının yüklenme kaydı. <see cref="Sha256"/> dosya içeriğinin özetidir: aynı içerik ikinci kez gelirse
/// yükleme atlanır. Ham dosya <c>data/arsiv/&lt;Sha256&gt;.xlsx</c> olarak saklanır.
/// </summary>
public class Yukleme
{
    public int Id { get; set; }

    public required string DosyaAdi { get; set; }

    /// <summary>Küçük harfli, 64 karakterlik onaltılık SHA256.</summary>
    public required string Sha256 { get; set; }

    public DateTimeOffset YuklenmeZamani { get; set; }

    public int SecimId { get; set; }
    public Secim Secim { get; set; } = null!;

    public OyTuru OyTuru { get; set; }

    public int IlceId { get; set; }
    public Ilce Ilce { get; set; } = null!;

    public int SatirSayisi { get; set; }
    public int SandikSayisi { get; set; }
    public int MahalleSayisi { get; set; }

    /// <summary>Bütün parti ve aday sütunlarının toplamı.</summary>
    public long ToplamOy { get; set; }

    /// <summary>DSP sütununun toplamı; dosyada DSP sütunu yoksa boş.</summary>
    public long? DspOyu { get; set; }

    public KontrolSonucu KontrolSonucu { get; set; }
    public string? KontrolDosyasiSha256 { get; set; }

    public YuklemeDurumu Durum { get; set; }
}
