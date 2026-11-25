using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Öncelik listesinde her satırın "neden öncelikli" cümlesi. Tek cümle, sabit kalıp, sayılar satırdan:
/// <c>AKÇABURGAZ MAH.: 2009'da %3,85 ile ilçe oranının (%0,98) üstündeydi ama geriledi (2024'te %0,32); tabanını
/// koruyabilseydi 434 oy daha alırdı (ilçenin 15. büyük mahallesi, 12.274 oy); ⚠ 2019 seçiminde sınır şüphesi var.</c>
/// Sayılara ek getirilmez (okunuşa bağlıdır); yalnızca yıllar ek alır (<see cref="Metinler.Bulunma"/>).
/// </summary>
public static class OncelikCumlesi
{
    public static string Yaz(OncelikSatiri s)
    {
        var g = s.Grup;
        if (g.Grup is not { } grup || g.Egilim is not { } egilim || g.SonYil is not { } sonYil)
            return $"{g.BirimAdi}: grup hesaplanamadı ({g.Not ?? "veri yok"}).";

        var konum = g.Gucluydu == true ? "üstündeydi" : "altındaydı";
        var seyir = (grup, egilim) switch
        {
            (MahalleGrubu.Erozyon, _) => "ama geriledi",
            (MahalleGrubu.Kale, Egilim.Kazandi) => "ve gücünü artırdı",
            (MahalleGrubu.Kale, _) => "ve gücünü korudu",
            (MahalleGrubu.Yukselen, _) => "ama yükseldi",
            (MahalleGrubu.Zayif, Egilim.Kaybetti) => "ve daha da geriledi",
            _ => "ve değişmedi",
        };
        // İki ondalıkta eşit görünen oranlar (0,305 ve 0,310 → %0,31) "altındaydı" ile çelişmesin: üç ondalık yazılır.
        var (taban, ilce) = Metinler.Oran(g.TabanOrani) == Metinler.Oran(g.IlceTabanOrani)
            ? (Uc(g.TabanOrani), Uc(g.IlceTabanOrani))
            : (Metinler.Oran(g.TabanOrani), Metinler.Oran(g.IlceTabanOrani));
        var cumle = $"{g.BirimAdi}: {Metinler.Bulunma(g.TabanYili)} %{taban} ile ilçe oranının " +
                    $"(%{ilce}) {konum} {seyir} ({Metinler.Bulunma(sonYil)} %{Metinler.Oran(g.SonOrani)})";

        var kayip = s.KayipOy ?? 0;
        cumle += kayip >= 1 ? $"; tabanını koruyabilseydi {Metinler.Oy(kayip)} oy daha alırdı"
            : kayip <= -1 ? $"; tabanının {Metinler.Oy(-kayip)} oy üstünde"
            : "; tabanıyla aynı düzeyde";

        if (s.BuyuklukSirasi is { } b && g.SonToplamOy is { } toplam)
            cumle += $" (ilçenin {(b == 1 ? "en" : $"{b}.")} büyük mahallesi, {Metinler.Oy(toplam)} oy)";

        if (s.SupheliYillar.Count > 0)
            cumle += $"; ⚠ {string.Join(", ", s.SupheliYillar)} seçiminde sınır şüphesi var";

        return cumle + ".";
    }

    private static string Uc(decimal? v) => v is null ? Metinler.Bos : v.Value.ToString("N3", Metinler.Turkce);
}
