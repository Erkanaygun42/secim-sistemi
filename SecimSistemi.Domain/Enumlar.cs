namespace SecimSistemi.Domain;

/// <summary>Seçimin türü. Cumhurbaşkanlığı ikinci turu ayrı tarihli ayrı bir seçimdir.</summary>
public enum SecimTuru
{
    Yerel,
    Genel,
}

/// <summary>Oy pusulasının türü. Veritabanında metin olarak saklanır.</summary>
public enum OyTuru
{
    BelediyeMeclisi,
    IlGenelMeclisi,
    BelediyeBaskani,
    BuyuksehirBaskani,
    Milletvekili,
    Cumhurbaskani,
}

/// <summary>Bir sonucun ya da oy özetinin hangi bölge düzeyinde geldiği. Büyükten küçüğe sıralı.</summary>
public enum BolgeDuzeyi
{
    Il,
    Ilce,
    MahalleKoy,
    Sandik,
}

public enum MahalleKoyTuru
{
    Mahalle,
    Koy,
}

/// <summary>DSP'nin bir seçimde, bir bölgedeki durumu. İttifak bilgisi ayrıca <see cref="DspKatilimi.IttifakAdi"/> alanında tutulur.</summary>
public enum DspDurumu
{
    KendiListesi,
    BaskaPartininListesi,
    Girmedi,
}

/// <summary>Eski bir bölge biriminin yenisine nasıl bağlandığı.</summary>
public enum EslemeTuru
{
    Birlesme,
    Bolunme,
    AdDegisikligi,
    KodDegisikligi,
}
