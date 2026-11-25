using System.Text.RegularExpressions;
using SecimSistemi.Importer.Harita;
using SecimSistemi.Importer.Okuma;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Mahalle haritası: OSM sınırlarının birleştirilmesi, GeoJSON, ad eşleştirme, renk sınıfları, SVG.</summary>
public class HaritaTestleri
{
    private static OsmParca P(params (double, double)[] n) => new(n);

    [Fact]
    public void Halka_ParcalarUcUcaBirlesir_TersParcaCevrilir_AcikSayilir()
    {
        // Kare: (0,0)→(1,0)→(1,1)→(0,1)→(0,0); üçüncü parça ters yönde. Ayrıca kapanmayan bir parça.
        var halkalar = OsmSinirlari.HalkalaraBirlestir(
            [P((0, 0), (1, 0)), P((1, 0), (1, 1)), P((0, 0), (0, 1), (1, 1)), P((5, 5), (6, 6))], out var acik);

        var h = Assert.Single(halkalar);
        Assert.Equal(h[0], h[^1]);
        Assert.Equal(5, h.Count);
        Assert.Equal(1, acik);
    }

    private const string Yanit = """
        {"elements":[
          {"type":"relation","id":11,"tags":{"name":"Akçaburgaz Mahallesi","admin_level":"8"},"members":[
            {"type":"way","role":"outer","geometry":[{"lat":41.0,"lon":28.6},{"lat":41.0,"lon":28.7}]},
            {"type":"way","role":"outer","geometry":[{"lat":41.0,"lon":28.7},{"lat":41.1,"lon":28.7},{"lat":41.0,"lon":28.6}]},
            {"type":"node","role":"admin_centre"}]},
          {"type":"relation","id":12,"tags":{"name":"Bozuk Mahallesi"},"members":[
            {"type":"way","role":"outer","geometry":[{"lat":41.2,"lon":28.6},{"lat":41.3,"lon":28.7}]}]}
        ]}
        """;

    [Fact]
    public void Coz_IliskiMahalle_KapanmayanAtlanir_GeoJsonGidipGelir()
    {
        var (mahalleler, uyarilar) = OsmSinirlari.Coz(Yanit);

        var m = Assert.Single(mahalleler);
        Assert.Equal((11L, "Akçaburgaz Mahallesi"), (m.OsmId, m.Ad));
        Assert.Contains(uyarilar, u => u.StartsWith("Bozuk Mahallesi (OSM 12): kapalı sınır kurulamadı"));

        var ilce = BolgeTanimlari.Ilceler["esenyurt"];
        var harita = IlceHaritasi.Oku(OsmSinirlari.GeoJson(ilce, mahalleler, new DateTime(2026, 11, 26)));
        Assert.Equal(("34-ESENYURT", "2026-11-26"), (harita.IlceKod, harita.Indirme));
        Assert.Contains("OpenStreetMap", harita.Lisans);
        Assert.Contains("ODbL", harita.Lisans);
        Assert.Equal(4, harita.Mahalleler.Single().Halkalar.Single().Count);
    }

    [Fact]
    public void Sorgu_IlinAltUyelerindenIlce_IlceAlanindanMahalleler()
    {
        var q = OsmSinirlari.Sorgu(BolgeTanimlari.Ilceler["sisli"]);

        Assert.Contains("[\"ISO3166-2\"=\"TR-34\"]", q);
        Assert.Contains("rel(r)[\"boundary\"=\"administrative\"][\"admin_level\"=\"6\"][\"name\"=\"Şişli\"]", q);
        Assert.Contains("rel(area.alan)[\"boundary\"=\"administrative\"][\"admin_level\"=\"8\"];out geom;", q);
    }

    [Theory]
    [InlineData("ESENYURT", "Esenyurt")]
    [InlineData("ŞİŞLİ", "Şişli")]
    [InlineData("BAKIRKÖY", "Bakırköy")]
    [InlineData("İSTANBUL", "İstanbul")]
    public void OsmAdi_TurkceBuyukKucuk(string ad, string beklenen) => Assert.Equal(beklenen, OsmSinirlari.OsmAdi(ad));

    [Theory]
    [InlineData("AKÇABURGAZ MAH.", "AKÇABURGAZ")]
    [InlineData("Akçaburgaz Mahallesi", "AKÇABURGAZ")]
    [InlineData("Necip Fazıl Kısakürek Mahallesi", "NECİP FAZIL KISAKÜREK")]
    [InlineData("İnönü Mahallesi", "İNÖNÜ")]
    public void Normal_EkVeBuyukHarf(string ad, string beklenen) => Assert.Equal(beklenen, HaritaCizici.Normal(ad));

    private static HaritaMahallesi M(long id, string ad) => new(id, ad, [[(0, 0), (1, 0), (1, 1), (0, 0)]]);

    [Fact]
    public void Eslestir_TamAd_HataliOsmAdiOnEkle_ElleEslemeOnce_EslesmeyenlerListelenir()
    {
        var harita = new IlceHaritasi("34-ESENYURT", "lisans", "2026-11-26",
            [M(1, "Akçaburgaz Mahallesi"), M(2, "Akevler Mahallesi 7 sokak"), M(3, "Eski Mahallesi"), M(4, "Yeni Ad Mahallesi")]);
        (int, string)[] birimler = [(119318, "AKÇABURGAZ MAH."), (147447, "AKEVLER MAH."), (900, "KAYIP MAH."), (901, "ESKİ AD MAH.")];

        var e = HaritaCizici.Eslestir(harita, birimler, [new HaritaEslemesi("34-ESENYURT", 4, 901)]);

        Assert.Equal(119318, e.OsmdenBirime[1]);
        Assert.Equal(147447, e.OsmdenBirime[2]);     // "AKEVLER 7 SOKAK" "AKEVLER " ile başlıyor
        Assert.Equal(901, e.OsmdenBirime[4]);        // elle
        Assert.Equal([3L], e.EslesmeyenOsm.Select(m => m.OsmId));
        Assert.Equal([(900, "KAYIP MAH.")], e.EslesmeyenYsk);
    }

    [Fact]
    public void Siniflar_BesteBirlikDilimler_AzDegerdeAzSinif()
    {
        var g = Gorunum.Varsayilan;
        var s = HaritaCizici.Siniflar([0.1m, 0.2m, 0.3m, 0.4m, 0.5m, 0.6m, 0.7m, 0.8m, 0.9m, 1.0m, 4m], g);

        Assert.Equal(5, s.Count);
        Assert.Equal((0.1m, 4m), (s[0].Alt, s[^1].Ust));
        Assert.Equal(s[^1].Renk, HaritaCizici.Renk(s, 4m));
        Assert.Equal(s[0].Renk, HaritaCizici.Renk(s, 0.1m));
        Assert.Null(HaritaCizici.Renk(s, null));
        Assert.NotEqual(s[0].Renk, s[^1].Renk);
        Assert.Single(HaritaCizici.Siniflar([0.5m, 0.5m], g));
        Assert.Empty(HaritaCizici.Siniflar([], g));
    }

    [Fact]
    public void Ciz_HerMahalleBirYol_EslesmeyenTarali_IpucuVar()
    {
        var harita = new IlceHaritasi("34-ESENYURT", "lisans", "2026-11-26", [M(1, "A Mahallesi"), M(2, "B Mahallesi")]);
        var s = HaritaCizici.Siniflar([0.3m], Gorunum.Varsayilan);

        var svg = HaritaCizici.Ciz(harita, id => id == 1 ? (0.3m, "A MAH.: %0,30", false) : null, s);

        Assert.Equal(2, Regex.Matches(svg, "<path ").Count);
        Assert.Contains("<title>A MAH.: %0,30</title>", svg);
        Assert.Contains("fill=\"url(#tarama)\"", svg);
        Assert.Contains("<title>B Mahallesi: YSK mahallesiyle eşleşmedi</title>", svg);
        Assert.Contains("fill-rule=\"evenodd\"", svg);

        var donemDisi = HaritaCizici.Ciz(harita, id => (0.3m, "x", id == 2), s);
        Assert.Contains($"fill=\"{HaritaCizici.DonemDisiRengi}\"", donemDisi);
    }

    [Theory]
    [InlineData("34-ESENYURT;999;119318", 1)]
    [InlineData("# yorum", 0)]
    [InlineData("ilce;osm_id;birim_kod;not", 0)]
    public void Eslemeler_Coz(string satir, int beklenen) => Assert.Equal(beklenen, HaritaDeposu.EslemeleriCoz([satir]).Count);

    [Fact]
    public void Eslemeler_BozukSatir_Hata() =>
        Assert.Throws<FormatException>(() => HaritaDeposu.EslemeleriCoz(["34-ESENYURT;abc;119318"]));

    [Fact]
    public void DepodakiEsenyurtHaritasi_43MahalleVeAtif()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SecimSistemi.Web", "Harita", "esenyurt.geojson");

        var h = IlceHaritasi.Oku(File.ReadAllText(Path.GetFullPath(yol)));

        Assert.Equal("34-ESENYURT", h.IlceKod);
        Assert.Equal(43, h.Mahalleler.Count);
        Assert.Contains("ODbL", h.Lisans);
        Assert.Equal(h.Mahalleler.Count, h.Mahalleler.Select(m => HaritaCizici.Normal(m.Ad)).Distinct().Count());
        Assert.All(h.Mahalleler, m => Assert.All(m.Halkalar, r => Assert.Equal(r[0], r[^1])));
    }
}
