using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Bağlantı bilgisi geliştirmede user-secrets'tan gelir ("ConnectionStrings:Secim"); depoya yazılmaz.
var baglanti = builder.Configuration.GetConnectionString("Secim")
    ?? throw new InvalidOperationException("'ConnectionStrings:Secim' tanımlı değil. dotnet user-secrets ile ekleyin.");

// Blazor Server bileşenleri uzun yaşadığı için DbContext doğrudan değil, fabrika üzerinden alınır.
builder.Services.AddDbContextFactory<SecimDbContext>(o => o.UseNpgsql(baglanti));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
