using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Tek renk ayarı ve SVG grafik çizimi.</summary>
public class GrafikVeGorunumTestleri
{
    private static IConfiguration Ayar(string? renk, string? ikinci = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Gorunum:AnaRenk"] = renk, ["Gorunum:IkinciRenk"] = ikinci }.Where(x => x.Value is not null)).Build();

    [Fact]
    public void AnaRenk_AyardanOkunur_YoksaLogoMavisi()
    {
        Assert.Equal("#16c0ed", Gorunum.Oku(Ayar(null)).AnaRenk);
        Assert.Equal("#003399", Gorunum.Oku(Ayar("#003399")).AnaRenk);
        Assert.Equal("#003399", Gorunum.Oku(Ayar("#003399")).SeriRengi(0)); // grafiklerin ilk serisi ana renk
    }

    [Fact]
    public void IkinciRenk_AyardanOkunur_YoksaLogoKirmizisi_GrafikteKullanilmaz()
    {
        var g = Gorunum.Oku(Ayar(null));

        Assert.Equal("#f04248", g.IkinciRenk);
        Assert.Equal("#aa0000", Gorunum.Oku(Ayar(null, "#AA0000")).IkinciRenk);
        Assert.DoesNotContain(g.IkinciRenk, g.SeriRenkleri);
        Assert.Contains("Gorunum:IkinciRenk", Assert.Throws<InvalidOperationException>(() => Gorunum.Oku(Ayar(null, "kırmızı"))).Message);
    }

    [Theory]
    [InlineData("mavi")]
    [InlineData("#12345")]
    [InlineData("16c0ed")]
    public void AnaRenk_GecersizseAnlasilirHata(string renk)
    {
        var e = Assert.Throws<InvalidOperationException>(() => Gorunum.Oku(Ayar(renk)));
        Assert.Contains("Gorunum:AnaRenk", e.Message);
    }

    [Theory]
    [InlineData("#16c0ed")]
    [InlineData("#f2a600")]
    [InlineData("#003399")]
    public void KoyuTon_AnaRenktenTuretilir_BeyazUzerindeOkunur(string renk)
    {
        var g = new Gorunum(renk);

        Assert.True(Gorunum.BeyazaKontrast(g.Koyu) >= 5.5, g.Koyu);
        if (Gorunum.BeyazaKontrast(renk) >= 5.5) Assert.Equal(renk, g.Koyu); // zaten koyuysa değişmez
    }

    private static readonly CizgiGrafigi Cizgi = new("T", null, [2009, 2014, 2019, 2024],
    [
        new("Belediye meclisi", 0, [1m, 0.8m, 0.5m, 0.3m]),
        new("Belediye başkanı", 1, [null, 0.4m, null, 0.2m]),
    ], DonemBaslangici: 2019);

    [Fact]
    public void Cizgi_BosDegerdeVeDonemSinirindaKesilir()
    {
        var svg = SvgCizici.Ciz(Cizgi, Gorunum.Varsayilan);

        // Meclis: 2009–2014 (dönem dışı, soluk) ve 2019–2024 iki parça; başkanın tek noktaları çizgi olmaz.
        var cizgiler = Regex.Matches(svg, "<polyline points=\"([^\"]+)\"[^>]*opacity=\"([^\"]+)\"");
        Assert.Equal(2, cizgiler.Count);
        Assert.Equal(("0.45", "1"), (cizgiler[0].Groups[2].Value, cizgiler[1].Groups[2].Value));
        Assert.Contains("karşılaştırılabilir dönem dışı", svg);
        Assert.Contains("<title>Belediye meclisi · 2009: %1,00 (karşılaştırılabilir dönem dışı)</title>", svg);
        Assert.Contains("<title>Belediye başkanı · 2014: %0,40 (karşılaştırılabilir dönem dışı)</title>", svg);
    }

    [Fact]
    public void Cizgi_GostergeUctakiDegerVeSeriRengi()
    {
        var svg = SvgCizici.Ciz(Cizgi, new Gorunum("#003399"));

        Assert.Contains(">Belediye meclisi</text>", svg);           // iki seride gösterge var
        Assert.Contains(">%0,30</text>", svg);                       // uçtaki değer
        Assert.Contains("stroke=\"#003399\"", svg);                  // meclis = ana renk
        Assert.Contains("stroke=\"#f47230\"", svg);                  // başkan = 2. renk
        Assert.Contains("<rect ", svg);                              // başkanın işareti kare (renkten başka ayırt edici)
        Assert.DoesNotContain("fill=\"#003399\">", svg);             // yazı seri renginde değil
    }

    [Fact]
    public void Cubuk_SiraEtiketDegerUyari()
    {
        var g = new CubukGrafigi("En çok", null, "oy",
            [new("AKÇABURGAZ MAH. (2009–2024)", 434m, "dönem 2009–2024", Uyari: true), new("B (2019–2024)", 12.6m)]);

        var svg = SvgCizici.Ciz(g, Gorunum.Varsayilan);

        Assert.True(svg.IndexOf("AKÇABURGAZ", StringComparison.Ordinal) < svg.IndexOf(">B (2019", StringComparison.Ordinal));
        Assert.Contains(">⚠ AKÇABURGAZ MAH. (2009–2024)</text>", svg);
        Assert.Contains(">434 oy</text>", svg);
        Assert.Contains(">13 oy</text>", svg);
        Assert.Contains("<title>AKÇABURGAZ MAH. (2009–2024): 434 oy · dönem 2009–2024</title>", svg);
    }

    [Theory]
    [InlineData(4.2, 1)]
    [InlineData(0.9, 0.2)]
    [InlineData(0.35, 0.1)]
    [InlineData(12, 2.5)]
    public void EksenAdimi_YuvarlakVeBesAdimdaKapanir(double enBuyuk, double beklenen) =>
        Assert.Equal((decimal)beklenen, SvgCizici.Adim((decimal)enBuyuk));
}
