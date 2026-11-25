using Microsoft.Extensions.Configuration;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Grupların gürültü eşiği tek ayarda (appsettings.json → Gruplama).</summary>
public class GrupAyarlariTestleri
{
    private static IConfiguration Ayar(params (string Anahtar, string Deger)[] d) =>
        new ConfigurationBuilder().AddInMemoryCollection(d.Select(x => new KeyValuePair<string, string?>(x.Anahtar, x.Deger))).Build();

    [Fact]
    public void Yoksa_Varsayilan_10OyVeYuzde30()
    {
        Assert.Equal(new GrupEsikleri(10m, 0.30m), GrupAyarlari.Oku(Ayar()));
        Assert.Equal("en az 10 oy ve oranın en az %30 değişmesi", GrupAyarlari.Metin(GrupEsikleri.Varsayilan));
    }

    [Fact]
    public void AyardanOkunur_KulturdenBagimsiz()
    {
        var e = GrupAyarlari.Oku(Ayar(("Gruplama:EnAzOy", "25"), ("Gruplama:EnAzGoreliDegisim", "0.5")));

        Assert.Equal(new GrupEsikleri(25m, 0.5m), e);
    }

    [Theory]
    [InlineData("Gruplama:EnAzGoreliDegisim", "30")]   // %30 yerine 30 yazılmış
    [InlineData("Gruplama:EnAzOy", "on")]
    public void GecersizAyar_AnlasilirHata(string anahtar, string deger)
    {
        var h = Assert.Throws<InvalidOperationException>(() => GrupAyarlari.Oku(Ayar((anahtar, deger))));
        Assert.Contains("Gruplama", h.Message);
    }

    [Fact]
    public void AppsettingsDosyasindakiDeger_Varsayilanla()
    {
        // Depodaki appsettings.json'un Gruplama bölümü okunabilir ve beklenen değerde.
        var yol = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SecimSistemi.Web", "appsettings.json");
        var ayar = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(yol)).Build();

        Assert.Equal(GrupEsikleri.Varsayilan, GrupAyarlari.Oku(ayar));
    }
}
