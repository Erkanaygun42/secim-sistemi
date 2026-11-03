using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;

namespace SecimSistemi.Data;

/// <summary>
/// Uygulamanın veritabanı bağlantısı. Tabloların ayrıntılı ayarları <c>Yapilandirma</c> klasöründeki sınıflardadır;
/// <see cref="OnModelCreating"/> onları otomatik bulur.
/// </summary>
public class SecimDbContext(DbContextOptions<SecimDbContext> options) : DbContext(options)
{
    public DbSet<Il> Iller => Set<Il>();
    public DbSet<Ilce> Ilceler => Set<Ilce>();
    public DbSet<MahalleKoy> MahalleKoyler => Set<MahalleKoy>();
    public DbSet<Sandik> Sandiklar => Set<Sandik>();
    public DbSet<IlceEslemesi> IlceEslemeleri => Set<IlceEslemesi>();
    public DbSet<MahalleKoyEslemesi> MahalleKoyEslemeleri => Set<MahalleKoyEslemesi>();

    public DbSet<Secim> Secimler => Set<Secim>();
    public DbSet<Parti> Partiler => Set<Parti>();
    public DbSet<PartiAdEslemesi> PartiAdEslemeleri => Set<PartiAdEslemesi>();
    public DbSet<Aday> Adaylar => Set<Aday>();
    public DbSet<DspKatilimi> DspKatilimlari => Set<DspKatilimi>();

    public DbSet<Sonuc> Sonuclar => Set<Sonuc>();
    public DbSet<OyOzeti> OyOzetleri => Set<OyOzeti>();

    /// <summary>PostgreSQL'in ICU Türkçe sıralaması: C &lt; Ç, I &lt; İ, O &lt; Ö, S &lt; Ş, U &lt; Ü.</summary>
    public const string TurkceSiralama = "tr-x-icu";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Bütün metin sütunları Türkçe sıralanır; ileride eklenen sütunlar da bu kuralı kendiliğinden alır.
        // Sıralama sütuna yazıldığı için veritabanının kuruluş ayarına bağlı kalmaz (test, canlı, yeni kurulum).
        configurationBuilder.Properties<string>().UseCollation(TurkceSiralama);

        // Seçenekler (enum) veritabanında sayı değil metin olarak durur: "BelediyeMeclisi" gibi.
        // Böylece SQL ile bakarken anlamı okunur, enum'a yeni değer eklemek eski kayıtları bozmaz.
        configurationBuilder.Properties<SecimTuru>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OyTuru>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<BolgeDuzeyi>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<MahalleKoyTuru>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DspDurumu>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<EslemeTuru>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SecimDbContext).Assembly);

        // Hiçbir silme işlemi bağlı kayıtları kendiliğinden silmesin: bir ili silmek
        // binlerce sonucu sessizce götürmemeli. Önce bağlı kayıtlar açıkça silinmeli.
        foreach (var iliski in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            iliski.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
