using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SecimSistemi.Data;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>
/// Entegrasyon testlerinin ortak veritabanı hazırlığı. Bütün test sınıfları için bir kez çalışır:
/// bağlantıyı user-secrets'tan okur ("ConnectionStrings:SecimTest") ve bekleyen migration'ları uygular.
/// Veritabanını silmez ya da yeniden oluşturmaz. Testler kendi transaction'larında çalışıp geri alındığı için
/// veritabanında kalıcı veri bırakmaz.
/// </summary>
public sealed class TestVeritabani : IAsyncLifetime
{
    public string Baglanti { get; }

    public TestVeritabani()
    {
        var ayarlar = new ConfigurationBuilder()
            .AddUserSecrets<TestVeritabani>()
            .Build();

        Baglanti = ayarlar.GetConnectionString("SecimTest")
            ?? throw new InvalidOperationException(
                "'ConnectionStrings:SecimTest' tanımlı değil. SecimSistemi.Tests projesinde dotnet user-secrets ile ekleyin.");

        // Güvenlik kilidi: testler yalnızca adı _test ile biten bir veritabanında çalışır, asıl veriye dokunmaz.
        var veritabani = new NpgsqlConnectionStringBuilder(Baglanti).Database;
        if (veritabani is null || !veritabani.EndsWith("_test", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Test bağlantısı '{veritabani}' veritabanını gösteriyor. Entegrasyon testleri yalnızca adı _test ile biten bir veritabanında çalışır.");
    }

    public SecimDbContext YeniContext() =>
        new(new DbContextOptionsBuilder<SecimDbContext>().UseNpgsql(Baglanti).Options);

    public async Task InitializeAsync()
    {
        await using var db = YeniContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>Veritabanı testleri bu koleksiyonda toplanır: hazırlık bir kez yapılır, testler sırayla çalışır.</summary>
[CollectionDefinition(Ad)]
public sealed class VeritabaniKoleksiyonu : ICollectionFixture<TestVeritabani>
{
    public const string Ad = "Veritabanı";
}

/// <summary>
/// Her test kendi transaction'ını açar; test bitince transaction geri alınır ve yazılan her şey silinir.
/// </summary>
[Collection(VeritabaniKoleksiyonu.Ad)]
public abstract class VeritabaniTesti(TestVeritabani veritabani) : IAsyncLifetime
{
    protected TestVeritabani Veritabani { get; } = veritabani;
    protected SecimDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Db = Veritabani.YeniContext();
        await Db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        // Commit yapılmadan kapatılan transaction geri alınır.
        await Db.DisposeAsync();
    }

    /// <summary>Kaydetmeyi dener ve PostgreSQL'in verdiği hatayı döndürür (örnek 23505 = tekillik ihlali).</summary>
    protected async Task<PostgresException> KaydetmeReddedilir()
    {
        var hata = await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
        return Assert.IsType<PostgresException>(hata.InnerException);
    }

    /// <summary>Ham SQL komutunu çalıştırır ve PostgreSQL'in verdiği hatayı döndürür.</summary>
    protected async Task<PostgresException> SqlReddedilir(FormattableString sql) =>
        await Assert.ThrowsAsync<PostgresException>(() => Db.Database.ExecuteSqlInterpolatedAsync(sql));
}
