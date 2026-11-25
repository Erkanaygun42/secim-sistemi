using System.Globalization;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Mahalle gruplarının gürültü eşiği tek ayarda durur: <c>appsettings.json</c> →
/// <c>"Gruplama": { "EnAzOy": 10, "EnAzGoreliDegisim": 0.30 }</c>. Değişim, oy farkı en az EnAzOy ve oranın göreli
/// değişimi en az EnAzGoreliDegisim (0,30 = %30) değilse "değişmedi" sayılır (<see cref="GrupHesabi"/>).
/// </summary>
public static class GrupAyarlari
{
    public static GrupEsikleri Oku(IConfiguration ayarlar)
    {
        var v = GrupEsikleri.Varsayilan;
        var esikler = new GrupEsikleri(
            Sayi(ayarlar, "Gruplama:EnAzOy") ?? v.EnAzOy,
            Sayi(ayarlar, "Gruplama:EnAzGoreliDegisim") ?? v.EnAzGoreliDegisim);
        try
        {
            esikler.Dogrula();
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new InvalidOperationException($"'Gruplama' ayarı geçersiz: {e.Message}", e);
        }
        return esikler;
    }

    // JSON sayısı ayarlara "0.3" olarak gelir; kültürden bağımsız okunur.
    private static decimal? Sayi(IConfiguration ayarlar, string anahtar) =>
        ayarlar[anahtar] is not { } metin ? null
        : decimal.TryParse(metin, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d
        : throw new InvalidOperationException($"'{anahtar}' ayarı sayı olmalı; verilen: '{metin}'.");

    /// <summary>Eşiğin sayfada ve dosyada yazılışı: "en az 10 oy ve oranın en az %30 değişmesi".</summary>
    public static string Metin(GrupEsikleri e) =>
        $"en az {e.EnAzOy.ToString("0.##", Metinler.Turkce)} oy ve oranın en az %{(e.EnAzGoreliDegisim * 100).ToString("0.##", Metinler.Turkce)} değişmesi";
}
