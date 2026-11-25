using MudBlazor;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Web.Components.Layout;

/// <summary>
/// Uygulamanın MudBlazor teması; renkler tek ayardan (<see cref="Gorunum"/>, appsettings.json → Gorunum:AnaRenk) türetilir.
/// Butonlar ve bağlantılar ana rengin koyu tonunu kullanır (logo mavisi beyaz yazıyla okunmaz); üst çubuk beyazdır,
/// altında ana renkte çizgi ve logo durur.
/// </summary>
public static class Tema
{
    public static MudTheme Olustur(Gorunum g) => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = g.Koyu,
            Secondary = g.AnaRenk,
            Info = g.Koyu,
            AppbarBackground = "#ffffff",
            AppbarText = "#1f1f1f",
        },
    };
}
