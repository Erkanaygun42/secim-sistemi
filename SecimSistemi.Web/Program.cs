using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SecimSistemi.Data;
using SecimSistemi.Web.Components;
using SecimSistemi.Web.Raporlar;

var builder = WebApplication.CreateBuilder(args);

// Bağlantı bilgisi geliştirmede user-secrets'tan gelir ("ConnectionStrings:Secim"); depoya yazılmaz.
var baglanti = builder.Configuration.GetConnectionString("Secim")
    ?? throw new InvalidOperationException("'ConnectionStrings:Secim' tanımlı değil. dotnet user-secrets ile ekleyin.");

// Blazor Server bileşenleri uzun yaşadığı için DbContext doğrudan değil, fabrika üzerinden alınır.
builder.Services.AddDbContextFactory<SecimDbContext>(o => o.UseNpgsql(baglanti));

builder.Services.AddMudServices();
builder.Services.AddScoped<KarneHazirlayici>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Sayılar ve tarihler Türkçe biçimde: ondalıkta virgül, binlikte nokta.
var turkce = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkce;
CultureInfo.DefaultThreadCurrentUICulture = turkce;

var app = builder.Build();

// Giriş ve yetki Sprint 7'de gelir. O zamana kadar uygulama yalnızca bu bilgisayardan açılabilir (docs/kararlar.md K16).
app.Use(async (context, next) =>
{
    var adres = context.Connection.RemoteIpAddress;
    if (adres is not null && !IPAddress.IsLoopback(adres))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Bu uygulama Sprint 7'ye kadar yalnızca localhost'tan açılabilir.");
        return;
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkce),
    SupportedCultures = [turkce],
    SupportedUICultures = [turkce],
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
