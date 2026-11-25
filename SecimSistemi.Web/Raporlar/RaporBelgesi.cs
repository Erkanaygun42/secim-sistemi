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
    /// <summary>Toplam satırı (kalın; Excel'de süzgecin dışında, en sonda).</summary>
    Toplam,
    /// <summary>Öne çıkan satır (kalın; örnek: meclis tablosunda DSP). Sıranın herhangi bir yerinde olabilir.</summary>
    Vurgu,
}

/// <param name="Tanim">Sütunun açıklaması ve örneği (<see cref="SutunSozlugu"/>); "Bu rapor nasıl okunur" bölümüne girer.</param>
public sealed record Sutun(string Baslik, HucreTuru Tur = HucreTuru.Metin, SutunTanimi? Tanim = null)
{
    public Sutun(SutunTanimi tanim, HucreTuru tur = HucreTuru.Metin) : this(tanim.Ad, tur, tanim) { }
}

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
/// <param name="DonemDisi">Karşılaştırılabilir dönemin dışında kalan hücrelerin sütun sıraları: boş ve gri yazılır.</param>
public sealed record RaporSatiri(IReadOnlyList<object?> Degerler, SatirTuru Tur = SatirTuru.Normal, IReadOnlySet<int>? DonemDisi = null)
{
    public bool DonemDisiMi(int sutun) => DonemDisi?.Contains(sutun) == true;
}

/// <summary>Raporun bir tablosu: Excel'de ayrı bir sayfa, PDF'te bir bölüm.</summary>
/// <param name="SayfaAdi">Excel sayfa adı (en çok 31 karakter).</param>
/// <param name="Grafikler">PDF'te tablonun üstünde çizilir; Excel'de grafiğin verisi tablonun kendisidir.</param>
public sealed record TabloBolumu(string Baslik, string SayfaAdi, string? Aciklama, IReadOnlyList<Sutun> Sutunlar,
    IReadOnlyList<RaporSatiri> Satirlar, IReadOnlyList<string> Notlar, IReadOnlyList<Grafik>? Grafikler = null)
{
    public IReadOnlyList<Grafik> GrafikListesi => Grafikler ?? [];
}

/// <summary>Raporun dayandığı yüklenmiş YSK dosyaları ve en son yükleme zamanı (Türkiye saati).</summary>
public sealed record KaynakListesi(IReadOnlyList<string> Dosyalar, DateTime? SonYukleme)
{
    public static readonly KaynakListesi Bos = new([], null);

    /// <summary>Başlıkta görünen kısa özet: "YSK Açık Veri Portalı, sandık düzeyi sonuçlar · 12 dosya · son yükleme 3 Kasım 2026".</summary>
    public string Ozet =>
        Metinler.VeriKaynagi
        + (Dosyalar.Count == 0 ? "" : $" · {Dosyalar.Count} dosya")
        + (SonYukleme is { } t ? $" · son yükleme {t.ToString("d MMMM yyyy", Metinler.Turkce)}" : "");
}

/// <summary>
/// Excel ve PDF'in ortak modeli. Ekrandaki raporla aynı veriden ve aynı sırayla kurulur (<see cref="RaporBelgeleri"/>).
/// Başlık bilgisi (filtre özeti, veri kaynağı, üretim tarihi, "partiye özel" ibaresi) her iki çıktıda da ilk yerde durur.
/// </summary>
/// <param name="Kaynaklar">Raporun dayandığı yüklenmiş YSK dosyaları. Başlıkta yalnızca özeti, sonda tam listesi yer alır.</param>
/// <param name="Uretim">Üretim zamanı, Türkiye saati.</param>
/// <param name="DosyaAdi">Uzantısız dosya adı.</param>
/// <param name="Uyari">Başlık bilgisinin hemen altında öne çıkan uyarı (örnek: meclis üye sayısı doğrulanmadı); boşsa yok.</param>
public sealed record RaporBelgesi(string Baslik, string FiltreOzeti, KaynakListesi Kaynaklar, DateTime Uretim,
    IReadOnlyList<TabloBolumu> Bolumler, IReadOnlyList<string> Notlar, string DosyaAdi, string? Uyari = null)
{
    public string UretimMetni => Uretim.ToString("d MMMM yyyy HH:mm", Metinler.Turkce) + " (Türkiye saati)";

    /// <summary>"Bu rapor nasıl okunur": rapordaki sütunların tanımları, ilk göründükleri sırayla, her biri bir kez.</summary>
    public IReadOnlyList<SutunTanimi> Okuma =>
        Bolumler.SelectMany(b => b.Sutunlar).Select(s => s.Tanim).OfType<SutunTanimi>().Distinct().ToList();
}
