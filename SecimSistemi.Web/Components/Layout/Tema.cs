using MudBlazor;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Web.Components.Layout;

/// <summary>
/// Uygulamanın MudBlazor teması; renkler tek ayardan (<see cref="Gorunum"/>, appsettings.json → Gorunum:AnaRenk) türetilir.
/// Üst çubuğun zemini ana renktir; üzerindeki yazı ve simgeler koyu lacivert (<see cref="Gorunum.BantYazisi"/>), çünkü
/// logo mavisi üzerinde beyaz okunmaz. Butonlar ve bağlantılar ana rengin koyu tonunu kullanır.
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
            AppbarBackground = g.AnaRenk,
            AppbarText = Gorunum.BantYazisi,
        },
    };
}
