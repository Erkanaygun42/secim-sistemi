namespace SecimSistemi.Domain.Analiz;

/// <summary>Mahallenin kendi karşılaştırılabilir dönemindeki seyri: dönem başında güçlü müydü, dönem içinde ne oldu.</summary>
public enum MahalleGrubu
{
    /// <summary>Güçlüydü, korudu (ya da kazandı).</summary>
    Kale,

    /// <summary>Güçlüydü, kaybetti.</summary>
    Erozyon,

    /// <summary>Zayıftı, kazandı.</summary>
    Yukselen,

    /// <summary>Zayıftı, kazanamadı (değişmedi ya da kaybetti).</summary>
    Zayif,
}

public enum Egilim { Kaybetti, Degismedi, Kazandi }

/// <summary>
/// Gürültü eşiği: bir değişimin "değişti" sayılması için oy farkı en az <see cref="EnAzOy"/> ve oranın göreli değişimi
/// en az <see cref="EnAzGoreliDegisim"/> (0,30 = %30) olmalı; ikisinden biri tutmazsa "değişmedi". Göreli eşik, DSP'nin
/// farklı güçte olduğu ilçelerde aynı işi görür (sabit puan eşiği güçlü ilçede küçük, zayıf ilçede büyük kalır).
/// Ayar: appsettings.json → Gruplama (Web).
/// </summary>
public sealed record GrupEsikleri(decimal EnAzOy, decimal EnAzGoreliDegisim)
{
    public static readonly GrupEsikleri Varsayilan = new(EnAzOy: 10m, EnAzGoreliDegisim: 0.30m);

    public void Dogrula()
    {
        if (EnAzOy < 0) throw new ArgumentOutOfRangeException(nameof(EnAzOy), EnAzOy, "Oy eşiği negatif olamaz.");
        if (EnAzGoreliDegisim is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(EnAzGoreliDegisim), EnAzGoreliDegisim, "Göreli eşik 0 ile 1 arasında olmalı (0,30 = %30).");
    }
}

/// <summary>Bir mahallenin grubu ve dayandığı değerler. Oranlar yüzde (0–100), puan yüzde puan.</summary>
/// <param name="IlceTabanOrani">İlçenin taban yılındaki oranı (aynı oy türü); "dönem başındaki güç" bununla ölçülür.</param>
/// <param name="IlceSonOrani">İlçenin son seçimdeki oranı; "bugünkü güç" bununla ölçülür.</param>
/// <param name="OyDegisimi">Gerçek oy − beklenen oy (= −kayıp oy); artı kazanç, eksi kayıp.</param>
/// <param name="GoreliDegisim">Son oran ÷ taban oranı − 1 (−0,92 = oran %92 düştü); taban oranı 0 ise boş.</param>
/// <param name="Grup">DSP taban ya da son seçimde pusulada değilse, ya da ilçe oranı yoksa boş (<see cref="Not"/>).</param>
public sealed record GrupSatiri(
    int BirimKod, string BirimAdi, int TabanYili, int? SonYil,
    decimal? TabanOrani, decimal? SonOrani, decimal? IlceTabanOrani, decimal? IlceSonOrani,
    decimal? PuanFarki, decimal? OyDegisimi, decimal? GoreliDegisim, long? SonToplamOy,
    bool? Gucluydu, Egilim? Egilim, MahalleGrubu? Grup, string? Not)
{
    /// <summary>Dönem başındaki güç: taban oranı − ilçenin taban oranı (puan).</summary>
    public decimal? DonemBasindakiGuc => TabanOrani - IlceTabanOrani;

    /// <summary>Bugünkü güç: son oran − ilçenin son oranı (puan). 2x2 tablonun dikey ekseni.</summary>
    public decimal? BugunkuGuc => SonOrani - IlceSonOrani;
}

/// <param name="Satirlar">Erime raporunun sırasıyla (dönem, sonra kayıp oy).</param>
public sealed record GrupSonucu(OyTuru OyTuru, GrupEsikleri Esikler, IReadOnlyList<GrupSatiri> Satirlar)
{
    /// <summary>Eşik yüzünden "değişmedi" sayılan mahalle sayısı (sayfada gösterilir).</summary>
    public int DegismeyenSayisi => Satirlar.Count(s => s.Egilim == Analiz.Egilim.Degismedi);

    public int Sayi(MahalleGrubu g) => Satirlar.Count(s => s.Grup == g);
}

/// <summary>
/// Mahalle grupları. Her mahalle kendi karşılaştırılabilir dönemine göre değerlendirilir (taban = dönemin ilk seçimi):
/// <list type="bullet">
/// <item>Güçlüydü: taban oranı ≥ ilçenin aynı yıldaki oranı (aynı oy türü).</item>
/// <item>Eğilim: oy değişimi (gerçek − beklenen) en az <see cref="GrupEsikleri.EnAzOy"/> oy VE göreli değişim en az
///   <see cref="GrupEsikleri.EnAzGoreliDegisim"/> ise yönüne göre Kazandı/Kaybetti; değilse Değişmedi. Eşiğe eşit değer değişmiş sayılır.
///   Taban oranı 0 ise göreli değişim tanımsızdır; yalnızca oy eşiğine bakılır.</item>
/// <item>Güçlüydü: Kaybetti → Erozyon, diğerleri → Kale. Zayıftı: Kazandı → Yükselen, diğerleri → Zayıf.</item>
/// <item>DSP taban ya da son seçimde pusulada değilse (oran boş) ya da ilçe oranı yoksa grup boştur.</item>
/// </list>
/// </summary>
public static class GrupHesabi
{
    /// <param name="ilceOranlari">Yıl → ilçenin oranı (yüzde), aynı oy türü (<see cref="ErimeHesabi.IlceErimesi"/>).</param>
    public static GrupSonucu Hesapla(ErimeRaporu rapor, IReadOnlyDictionary<int, decimal?> ilceOranlari, GrupEsikleri esikler)
    {
        esikler.Dogrula();
        var ilceSon = rapor.SonYil is { } y ? ilceOranlari.GetValueOrDefault(y) : null;
        var satirlar = rapor.Satirlar.Select(s => Satir(s, rapor.SonYil, ilceOranlari.GetValueOrDefault(s.TabanYili), ilceSon, esikler)).ToList();
        return new GrupSonucu(rapor.OyTuru, esikler, satirlar);
    }

    public static Egilim EgilimBul(decimal oyDegisimi, decimal? goreliDegisim, GrupEsikleri e)
    {
        var oyYeterli = Math.Abs(oyDegisimi) >= e.EnAzOy;
        var oranYeterli = goreliDegisim is not { } g || Math.Abs(g) >= e.EnAzGoreliDegisim;
        if (!oyYeterli || !oranYeterli || oyDegisimi == 0) return Egilim.Degismedi;
        return oyDegisimi > 0 ? Egilim.Kazandi : Egilim.Kaybetti;
    }

    public static MahalleGrubu GrupBul(bool gucluydu, Egilim egilim) => (gucluydu, egilim) switch
    {
        (true, Egilim.Kaybetti) => MahalleGrubu.Erozyon,
        (true, _) => MahalleGrubu.Kale,
        (false, Egilim.Kazandi) => MahalleGrubu.Yukselen,
        (false, _) => MahalleGrubu.Zayif,
    };

    private static GrupSatiri Satir(ErimeSatiri s, int? sonYil, decimal? ilceTaban, decimal? ilceSon, GrupEsikleri e)
    {
        decimal? oyDegisimi = -s.KayipOy;
        decimal? goreli = s.TabanOrani > 0 && s.SonOrani is not null ? s.SonOrani / s.TabanOrani - 1 : null;

        // Taban oranı 0 olan birimde erime notu (korunma hesaplanamaz) grubu engellemez: oy değişimi bellidir.
        string? not =
            s.TabanOrani is null || s.SonOrani is null || s.KayipOy is null ? s.Not ?? "taban ya da son seçimde oran yok" :
            ilceTaban is null ? $"ilçenin {s.TabanYili} oranı yok (DSP pusulada değil)" :
            null;

        bool? gucluydu = null;
        Egilim? egilim = null;
        MahalleGrubu? grup = null;
        if (not is null && s.TabanOrani is { } taban && oyDegisimi is { } od)
        {
            gucluydu = taban >= ilceTaban;
            egilim = EgilimBul(od, goreli, e);
            grup = GrupBul(gucluydu.Value, egilim.Value);
        }

        return new GrupSatiri(s.BirimKod, s.BirimAdi, s.TabanYili, sonYil, s.TabanOrani, s.SonOrani, ilceTaban, ilceSon,
            s.PuanFarki, oyDegisimi, goreli, s.SonToplamOy, gucluydu, egilim, grup, not);
    }
}
