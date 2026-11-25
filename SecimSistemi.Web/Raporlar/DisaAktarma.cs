namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Adresteki rapor adı ve filtrelerden <see cref="RaporBelgesi"/> hazırlar. Sayfalarla aynı hazırlayıcıları kullanır;
/// böylece dosyada ekrandaki tablo çıkar. Raporlar: karne, mahalle (ilce, birim), erime.
/// </summary>
public sealed class BelgeHazirlayici(KarneHazirlayici karne, ErimeHazirlayici erime, RaporKaynaklari kaynaklar, TimeProvider zaman)
{
    public async Task<RaporBelgesi?> HazirlaAsync(string rapor, IQueryCollection q, CancellationToken ct = default)
    {
        var uretim = Metinler.TurkiyeSaati(zaman.GetUtcNow());
        switch (rapor)
        {
            case "karne":
            {
                var s = await karne.HazirlaAsync(KarneSorgusu.Oku(q), ct);
                if (s.Karne is not { } k) return null;
                var dosyalar = await kaynaklar.ListeleAsync(k.Ilce.Id, k.Secim.Id, k.OyTuru, ct);
                return RaporBelgeleri.Karne(s, dosyalar, uretim);
            }
            case "mahalle":
            {
                if (q["ilce"].ToString() is not { Length: > 0 } ilceKod || !int.TryParse(q["birim"], out var birim)) return null;
                if (await karne.MahalleAsync(ilceKod, birim, ct) is not { } d) return null;
                return RaporBelgeleri.Mahalle(d, await kaynaklar.ListeleAsync(d.Ilce.Id, ct: ct), uretim);
            }
            case "erime":
            {
                var e = await erime.HazirlaAsync(ErimeSorgusu.Oku(q), ct);
                if (e.Ilce is null || e.Seriler.Count == 0) return null;
                return RaporBelgeleri.Erime(e, await kaynaklar.ListeleAsync(e.Ilce.Id, ct: ct), uretim);
            }
            default:
                return null;
        }
    }
}

public static class DisaAktarmaUclari
{
    public static void AddDisaAktarma(this IServiceCollection s)
    {
        s.AddSingleton(TimeProvider.System);
        // Ana renk tek ayarda: appsettings.json → Gorunum:AnaRenk (tema, PDF, Excel ve grafikler buradan okur).
        s.AddSingleton(sp => Gorunum.Oku(sp.GetRequiredService<IConfiguration>()));
        // Mahalle gruplarının gürültü eşiği: appsettings.json → Gruplama.
        s.AddSingleton(sp => GrupAyarlari.Oku(sp.GetRequiredService<IConfiguration>()));
        s.AddScoped<KarneHazirlayici>();
        s.AddScoped<ErimeHazirlayici>();
        s.AddScoped<RaporKaynaklari>();
        s.AddScoped<BelgeHazirlayici>();
        s.AddScoped<GrupHazirlayici>();
        s.AddScoped<MeclisHazirlayici>();
        // Meclis üye sayıları: Tanimlar/meclis-uye-sayilari.csv (Importer'dan uygulamanın çıktı klasörüne kopyalanır).
        s.AddSingleton(_ => MeclisUyeTanimlari.Yukle());
        s.AddSingleton<PdfUretici>();
    }

    /// <summary><c>GET /disa-aktar/{rapor}.xlsx?...</c> ve <c>.pdf?...</c> (karne, mahalle, erime).</summary>
    public static void MapDisaAktarma(this WebApplication app)
    {
        app.MapGet("/disa-aktar/{rapor}.xlsx", async (string rapor, HttpRequest istek, BelgeHazirlayici h, Gorunum g, CancellationToken ct) =>
            await h.HazirlaAsync(rapor, istek.Query, ct) is { } b
                ? Results.File(ExcelYazici.Yaz(b, g), ExcelYazici.IcerikTuru, b.DosyaAdi + ".xlsx")
                : Results.NotFound("Rapor ya da filtre bulunamadı."));

        app.MapGet("/disa-aktar/{rapor}.pdf", async (string rapor, HttpRequest istek, BelgeHazirlayici h, PdfUretici pdf, CancellationToken ct) =>
            await h.HazirlaAsync(rapor, istek.Query, ct) is { } b
                ? Results.File(await pdf.UretAsync(b, ct), PdfUretici.IcerikTuru, b.DosyaAdi + ".pdf")
                : Results.NotFound("Rapor ya da filtre bulunamadı."));
    }
}
