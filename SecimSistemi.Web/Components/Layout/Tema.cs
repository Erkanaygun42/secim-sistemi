using MudBlazor;

namespace SecimSistemi.Web.Components.Layout;

/// <summary>Uygulamanın MudBlazor teması. Renkler sade tutuldu; parti rengi (kırmızı) birincil renk.</summary>
public static class Tema
{
    public static readonly MudTheme Uygulama = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#c8102e",
            AppbarBackground = "#c8102e",
        },
    };
}
