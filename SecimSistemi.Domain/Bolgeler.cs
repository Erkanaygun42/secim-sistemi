namespace SecimSistemi.Domain;

/// <summary>İl. <see cref="Kod"/> plaka kodudur (İstanbul = 34) ve değişmez.</summary>
public class Il
{
    public int Id { get; set; }
    public required int Kod { get; set; }
    public required string Ad { get; set; }
}

/// <summary>
/// İlçe. <see cref="Kod"/> şimdilik geçicidir: plaka ve ilçe adı ("34-ESENYURT").
/// Resmî ilçe koduna geçiş ileride eşleme/migration ile yapılacak.
/// </summary>
public class Ilce
{
    public int Id { get; set; }
    public required string Kod { get; set; }
    public required string Ad { get; set; }

    public int IlId { get; set; }
    public Il Il { get; set; } = null!;
}

/// <summary>
/// Mahalle ya da köy. <see cref="Kod"/> YSK dosyalarındaki "Muhtarlık Id" değeridir.
/// YSK'daki "Muhtarlık" sütunu bu birimdir, muhtar seçimi değildir.
/// </summary>
public class MahalleKoy
{
    public int Id { get; set; }
    public required int Kod { get; set; }
    public required string Ad { get; set; }
    public MahalleKoyTuru Tur { get; set; }

    public int IlceId { get; set; }
    public Ilce Ilce { get; set; } = null!;
}

/// <summary>
/// Sandık. Kalıcı kodu yoktur: sandık numaraları seçimden seçime aynı sandığı göstermez.
/// Bu yüzden her sandık bir seçime bağlıdır; yıllar arası karşılaştırma mahalle/köy düzeyinde yapılır.
/// </summary>
public class Sandik
{
    public int Id { get; set; }
    public required int No { get; set; }

    public int SecimId { get; set; }
    public Secim Secim { get; set; } = null!;

    public int MahalleKoyId { get; set; }
    public MahalleKoy MahalleKoy { get; set; } = null!;
}
