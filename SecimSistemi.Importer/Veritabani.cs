using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SecimSistemi.Data;

namespace SecimSistemi.Importer;

/// <summary>Importer komutlarının ortak veritabanı bağlantısı.</summary>
internal static class Veritabani
{
    /// <summary>
    /// Bağlantı bilgisi Web projesiyle aynı user-secrets kimliğinden okunur ("ConnectionStrings:Secim");
    /// csproj'daki UserSecretsId iki projede aynıdır. Bekleyen migration varsa durulur:
    /// Importer asıl veritabanına kendiliğinden migration uygulamaz.
    /// </summary>
    public static async Task<DbContextOptions<SecimDbContext>> HazirlaAsync()
    {
        var ayarlar = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Veritabani).Assembly)
            .Build();
        var baglanti = ayarlar.GetConnectionString("Secim")
            ?? throw new YuklemeHatasi("'ConnectionStrings:Secim' tanımlı değil. Web projesinde dotnet user-secrets ile ekleyin; Importer aynı kaydı okur.");
        var secenekler = new DbContextOptionsBuilder<SecimDbContext>().UseNpgsql(baglanti).Options;

        await using var db = new SecimDbContext(secenekler);
        var bekleyen = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (bekleyen.Count > 0)
            throw new YuklemeHatasi(
                $"Veritabanında uygulanmamış {bekleyen.Count} migration var ({string.Join(", ", bekleyen)}). Önce şunu çalıştırın:{Environment.NewLine}" +
                "  dotnet ef database update --project SecimSistemi.Data --startup-project SecimSistemi.Web");
        return secenekler;
    }

    /// <summary>Depodaki kaynak dosyanın yolu (bin klasöründeki kopyası değil): SecimSistemi.sln aranarak bulunur.</summary>
    public static string DepoYolu(params string[] parcalar)
    {
        for (var klasor = new DirectoryInfo(AppContext.BaseDirectory); klasor is not null; klasor = klasor.Parent)
        {
            if (File.Exists(Path.Combine(klasor.FullName, "SecimSistemi.sln")))
                return Path.Combine([klasor.FullName, .. parcalar]);
        }
        throw new YuklemeHatasi("Depo klasörü bulunamadı (SecimSistemi.sln yok). Komutu depo içinden çalıştırın.");
    }
}
