namespace SecimSistemi.Web.Raporlar;

/// <summary>Hücrenin türü: Excel'de sayı biçimini, PDF'te metne çevirmeyi ve hizalamayı belirler.</summary>
public enum HucreTuru
{
    Metin,
    /// <summary>Oy, sandık sayısı: 12.345.</summary>
    Tamsayi,
    /// <summary>Yüzde (0–100), iki ondalık: 0,98.</summary>
    Oran,
    /// <summary>Yüzde puan farkı, işaretli: +0,16.</summary>
    Puan,
    /// <summary>Kat (korunma oranı), iki ondalık: 0,21.</summary>
    Katsayi,
    /// <summary>Yıl, mahalle kodu, sıra numarası: binlik ayracı olmayan tamsayı.</summary>
    Kod,
}

public enum SatirTuru
{
    Normal,
    /// <summary>Sınır şüphesi olan satır (sarı).</summary>
    Uyari,
    /// <summary>Grup başlığı (dönem, seri); yalnızca ilk hücre doludur.</summary>
    Grup,
    /// <summary>Toplam satırı (kalın).</summary>
    Toplam,
}

public sealed record Sutun(string Baslik, HucreTuru Tur = HucreTuru.Metin);

/// <summary>Hücre değerinin metni (PDF'te). Biçimler ekrandakiyle aynıdır; boş sayı "—", boş metin boş kalır.</summary>
public static class HucreMetni
{
    public static string Yaz(HucreTuru tur, object? deger)
    {
        if (deger is null) return tur == HucreTuru.Metin ? "" : Metinler.Bos;
        if (deger is string s) return s;
        var d = Convert.ToDecimal(deger, Metinler.Turkce);
        return tur switch
        {
            HucreTuru.Tamsayi => Metinler.Oy(d),
            HucreTuru.Oran => Metinler.Oran(d),
            HucreTuru.Puan => Metinler.Puan(d),
            HucreTuru.Katsayi => Metinler.Katsayi(d),
            HucreTuru.Kod => d.ToString("0", Metinler.Turkce),
            _ => Convert.ToString(deger, Metinler.Turkce) ?? "",
        };
    }
}

/// <summary>Bir satırın değerleri; boş değer (null) "—" olarak yazılır.</summary>
public sealed record RaporSatiri(IReadOnlyList<object?> Degerler, SatirTuru Tur = SatirTuru.Normal);

/// <summary>Raporun bir tablosu: Excel'de ayrı bir sayfa, PDF'te bir bölüm.</summary>
/// <param name="SayfaAdi">Excel sayfa adı (en çok 31 karakter).</param>
public sealed record TabloBolumu(string Baslik, string SayfaAdi, string? Aciklama, IReadOnlyList<Sutun> Sutunlar,
    IReadOnlyList<RaporSatiri> Satirlar, IReadOnlyList<string> Notlar);

/// <summary>
/// Excel ve PDF'in ortak modeli. Ekrandaki raporla aynı veriden ve aynı sırayla kurulur (<see cref="RaporBelgeleri"/>).
/// Başlık bilgisi (filtre özeti, veri kaynağı, üretim tarihi, "partiye özel" ibaresi) her iki çıktıda da ilk yerde durur.
/// </summary>
/// <param name="Kaynaklar">Raporun dayandığı yüklenmiş YSK dosyaları: "dosya adı (yükleme tarihi)".</param>
/// <param name="Uretim">Üretim zamanı, Türkiye saati.</param>
/// <param name="DosyaAdi">Uzantısız dosya adı.</param>
public sealed record RaporBelgesi(string Baslik, string FiltreOzeti, IReadOnlyList<string> Kaynaklar, DateTime Uretim,
    IReadOnlyList<TabloBolumu> Bolumler, IReadOnlyList<string> Notlar, string DosyaAdi)
{
    public string UretimMetni => Uretim.ToString("d MMMM yyyy HH:mm", Metinler.Turkce) + " (Türkiye saati)";
}
