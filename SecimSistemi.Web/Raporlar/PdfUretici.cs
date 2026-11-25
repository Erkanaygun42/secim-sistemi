using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Playwright;
using SecimSistemi.Web.Components.Pdf;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// <see cref="RaporBelgesi"/>'nden PDF üretir: <see cref="PdfSablonu"/> HtmlRenderer ile HTML metnine çevrilir,
/// Playwright'ın Chromium'u bu metni yükleyip A4 yatay basar. Sunucu kendine istek atmaz; giriş eklendiğinde
/// (Sprint 7) çerez ya da oturum gerekmez. Chromium uygulama boyunca bir kez açılır (singleton), her PDF ayrı bir
/// tarayıcı bağlamında basılır.
/// Chromium kurulumu (bir kez): <c>powershell -ExecutionPolicy Bypass -File SecimSistemi.Web/bin/Debug/net10.0/playwright.ps1 install chromium</c>
/// </summary>
public sealed class PdfUretici(IServiceScopeFactory kapsamlar, ILoggerFactory gunluk, Gorunum gorunum) : IAsyncDisposable
{
    public const string IcerikTuru = "application/pdf";

    private readonly SemaphoreSlim _kilit = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _tarayici;

    /// <summary>Sayfa altı (her sayfada): ibare, rapor adı, sayfa numarası. Chromium'un kendi sınıfları: pageNumber, totalPages.</summary>
    private string Alt(RaporBelgesi b) =>
        "<div style=\"font-family: Segoe UI, Arial, sans-serif; font-size: 7pt; color: #666; width: 100%; padding: 0 10mm; display: flex; justify-content: space-between;\">" +
        $"<span style=\"color: {gorunum.Koyu}; font-weight: 700;\">{Kacis(Metinler.PartiyeOzel)}</span>" +
        $"<span>{Kacis(b.Baslik)} · {Kacis(b.UretimMetni)}</span>" +
        "<span>Sayfa <span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></span></div>";

    private static string Kacis(string s) => System.Net.WebUtility.HtmlEncode(s);

    public async Task<string> HtmlAsync(RaporBelgesi belge)
    {
        // HtmlRenderer kapsamlı (scoped) servis ister; PdfUretici singleton olduğu için her çizimde kapsam açılır.
        await using var kapsam = kapsamlar.CreateAsyncScope();
        await using var cizici = new HtmlRenderer(kapsam.ServiceProvider, gunluk);
        return await cizici.Dispatcher.InvokeAsync(async () =>
        {
            var parametreler = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(PdfSablonu.Belge)] = belge });
            var cikti = await cizici.RenderComponentAsync<PdfSablonu>(parametreler);
            return cikti.ToHtmlString();
        });
    }

    public async Task<byte[]> UretAsync(RaporBelgesi belge, CancellationToken ct = default)
    {
        var html = await HtmlAsync(belge);
        var tarayici = await TarayiciAsync(ct);

        await using var baglam = await tarayici.NewContextAsync();
        var sayfa = await baglam.NewPageAsync();
        await sayfa.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });
        return await sayfa.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            Landscape = true,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<div></div>",
            FooterTemplate = Alt(belge),
            Margin = new Margin { Top = "10mm", Bottom = "14mm", Left = "10mm", Right = "10mm" },
        });
    }

    private async Task<IBrowser> TarayiciAsync(CancellationToken ct)
    {
        if (_tarayici is { IsConnected: true } t) return t;
        await _kilit.WaitAsync(ct);
        try
        {
            if (_tarayici is { IsConnected: true } t2) return t2;
            _playwright ??= await Playwright.CreateAsync();
            try
            {
                _tarayici = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (PlaywrightException e) when (e.Message.Contains("Executable doesn't exist", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PDF için Chromium kurulu değil. Bir kez çalıştırın: " +
                    "powershell -ExecutionPolicy Bypass -File SecimSistemi.Web/bin/Debug/net10.0/playwright.ps1 install chromium", e);
            }
            return _tarayici;
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_tarayici is not null) await _tarayici.CloseAsync();
        _playwright?.Dispose();
        _kilit.Dispose();
    }
}
