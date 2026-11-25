using System.Globalization;
using System.Text;
using System.Text.Json;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Harita;

/// <summary>Bir OSM sınır parçası (way): sırasıyla noktalar (boylam, enlem).</summary>
public sealed record OsmParca(IReadOnlyList<(double Lon, double Lat)> Noktalar);

/// <summary>OSM'deki bir mahalle sınırı (admin_level=8 ilişkisi): kapalı halkalar (dış ve iç; çizimde even-odd ile delik olur).</summary>
public sealed record OsmMahalle(long OsmId, string Ad, IReadOnlyList<IReadOnlyList<(double Lon, double Lat)>> Halkalar);

/// <summary>
/// İlçenin mahalle sınırlarını OpenStreetMap'ten (Overpass API) alır, kapalı halkalara birleştirir ve GeoJSON olarak kaydeder
/// (docs/kararlar.md K45). Harita verisi © OpenStreetMap katkıda bulunanlar, ODbL. Bir ilçe için tek sorgu; sunucu doluysa
/// bekleyip yansıyı dener. İstek başlığında kişisel bilgi yoktur.
/// </summary>
public static class OsmSinirlari
{
    public const string Atif = "Harita verisi © OpenStreetMap katkıda bulunanlar, ODbL 1.0 (https://www.openstreetmap.org/copyright)";

    private static readonly string[] Sunucular = ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter"];

    /// <summary>İlçe adının OSM'deki yazımı: "ŞİŞLİ" → "Şişli", "BAKIRKÖY" → "Bakırköy".</summary>
    public static string OsmAdi(string ad) => new CultureInfo("tr-TR").TextInfo.ToTitleCase(ad.ToLower(new CultureInfo("tr-TR")));

    /// <summary>
    /// İlçenin (admin_level 6) içindeki bütün mahalleler (admin_level 8), geometrileriyle. İlçe, ilin (ISO 3166-2, plaka:
    /// TR-34) OSM kaydının alt üyeleri arasından adıyla bulunur; il sınırından alan üretilemediği için (İstanbul'da denizi de
    /// kapsayan sınır) il alanı kullanılmaz. Mahalleler ilçe alanının içinden alınır.
    /// </summary>
    public static string Sorgu(IlceTanimi ilce) =>
        "[out:json][timeout:120];" +
        $"rel[\"boundary\"=\"administrative\"][\"ISO3166-2\"=\"TR-{ilce.Il.Kod:00}\"];" +
        $"rel(r)[\"boundary\"=\"administrative\"][\"admin_level\"=\"6\"][\"name\"=\"{OsmAdi(ilce.Ad)}\"]->.ilce;" +
        ".ilce map_to_area->.alan;" +
        "rel(area.alan)[\"boundary\"=\"administrative\"][\"admin_level\"=\"8\"];out geom;";

    public static async Task<string> IndirAsync(string sorgu, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SecimSistemi/1.0 (veri analizi)");
        for (var deneme = 1; deneme <= 3; deneme++)
            foreach (var sunucu in Sunucular)
            {
                try
                {
                    using var yanit = await http.PostAsync(sunucu, new FormUrlEncodedContent([new("data", sorgu)]), ct);
                    var metin = await yanit.Content.ReadAsStringAsync(ct);
                    if (yanit.IsSuccessStatusCode && metin.TrimStart().StartsWith('{') && IliskiVar(metin)) return metin;
                    Console.WriteLine(yanit.IsSuccessStatusCode && metin.TrimStart().StartsWith('{')
                        ? $"  {sunucu}: boş yanıt (bu sunucu sorguyu desteklemiyor olabilir); diğeri deneniyor…"
                        : $"  {sunucu}: {(int)yanit.StatusCode} (sunucu dolu ya da hata); bekleniyor…");
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"  {sunucu}: {e.Message}; bekleniyor…");
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    Console.WriteLine($"  {sunucu}: zaman aşımı; bekleniyor…");
                }
                await Task.Delay(TimeSpan.FromSeconds(10 * deneme), ct);
            }
        throw new YuklemeHatasi("Overpass sunucularından sınır alınamadı (sunucular dolu ya da yanıt boş). Birkaç dakika sonra yeniden deneyin; " +
            "ilçe adı OSM'deki yazımla eşleşmiyorsa docs/yeni-ilce-ekleme.md'ye bakın.");
    }

    /// <summary>Yanıtta en az bir ilişki (sınır) var mı.</summary>
    public static bool IliskiVar(string json)
    {
        using var b = JsonDocument.Parse(json);
        return b.RootElement.TryGetProperty("elements", out var e) && e.EnumerateArray().Any(x => x.GetProperty("type").GetString() == "relation");
    }

    /// <summary>Overpass yanıtındaki (out geom) ilişkileri mahallelere çevirir; halka kurulamayan ilişki atlanır ve bildirilir.</summary>
    public static (List<OsmMahalle> Mahalleler, List<string> Uyarilar) Coz(string json)
    {
        using var belge = JsonDocument.Parse(json);
        var mahalleler = new List<OsmMahalle>();
        var uyarilar = new List<string>();
        foreach (var e in belge.RootElement.GetProperty("elements").EnumerateArray())
        {
            if (e.GetProperty("type").GetString() != "relation") continue;
            var id = e.GetProperty("id").GetInt64();
            var ad = e.TryGetProperty("tags", out var t) && t.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var parcalar = new List<OsmParca>();
            foreach (var m in e.GetProperty("members").EnumerateArray())
            {
                if (m.GetProperty("type").GetString() != "way" || !m.TryGetProperty("geometry", out var g)) continue;
                var rol = m.TryGetProperty("role", out var r) ? r.GetString() : "";
                if (rol is not ("outer" or "inner" or "")) continue;
                parcalar.Add(new OsmParca([.. g.EnumerateArray().Select(p => (p.GetProperty("lon").GetDouble(), p.GetProperty("lat").GetDouble()))]));
            }
            var halkalar = HalkalaraBirlestir(parcalar, out var acik);
            if (halkalar.Count == 0) { uyarilar.Add($"{ad} (OSM {id}): kapalı sınır kurulamadı; atlandı."); continue; }
            if (acik > 0) uyarilar.Add($"{ad} (OSM {id}): {acik} sınır parçası kapalı halkaya bağlanamadı; çizimde eksik olabilir.");
            mahalleler.Add(new OsmMahalle(id, ad, halkalar));
        }
        return (mahalleler.OrderBy(m => m.Ad, StringComparer.Create(new CultureInfo("tr-TR"), true)).ToList(), uyarilar);
    }

    /// <summary>
    /// Sınır parçalarını uç noktalarından birleştirip kapalı halkalar kurar (gerekirse parçayı ters çevirir).
    /// Kapanmayan parçaların sayısı <paramref name="acikKalan"/>'dadır.
    /// </summary>
    public static List<IReadOnlyList<(double Lon, double Lat)>> HalkalaraBirlestir(IEnumerable<OsmParca> parcalar, out int acikKalan)
    {
        var kalan = parcalar.Where(p => p.Noktalar.Count >= 2).Select(p => p.Noktalar.ToList()).ToList();
        var halkalar = new List<IReadOnlyList<(double, double)>>();
        acikKalan = 0;
        while (kalan.Count > 0)
        {
            var halka = kalan[0];
            kalan.RemoveAt(0);
            while (halka[0] != halka[^1])
            {
                var son = halka[^1];
                var i = kalan.FindIndex(p => p[0] == son || p[^1] == son);
                if (i < 0) break;
                var ek = kalan[i];
                kalan.RemoveAt(i);
                if (ek[0] != son) ek.Reverse();
                halka.AddRange(ek.Skip(1));
            }
            if (halka[0] == halka[^1] && halka.Count >= 4) halkalar.Add(halka);
            else acikKalan++;
        }
        return halkalar;
    }

    /// <summary>GeoJSON (FeatureCollection; her mahalle bir Polygon, halkalar even-odd). Koordinatlar 5 ondalık (~1 m).</summary>
    public static string GeoJson(IlceTanimi ilce, IReadOnlyList<OsmMahalle> mahalleler, DateTime indirme)
    {
        using var akis = new MemoryStream();
        using (var w = new Utf8JsonWriter(akis, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("type", "FeatureCollection");
            w.WriteString("ilce", ilce.Kod);
            w.WriteString("kaynak", "OpenStreetMap (Overpass API), admin_level=8");
            w.WriteString("lisans", Atif);
            w.WriteString("indirme", indirme.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            w.WriteStartArray("features");
            foreach (var m in mahalleler)
            {
                w.WriteStartObject();
                w.WriteString("type", "Feature");
                w.WriteStartObject("properties");
                w.WriteNumber("osm_id", m.OsmId);
                w.WriteString("ad", m.Ad);
                w.WriteEndObject();
                w.WriteStartObject("geometry");
                w.WriteString("type", "Polygon");
                w.WriteStartArray("coordinates");
                foreach (var h in m.Halkalar)
                {
                    w.WriteStartArray();
                    foreach (var (lon, lat) in h)
                    {
                        w.WriteStartArray();
                        w.WriteNumberValue(Math.Round(lon, 5));
                        w.WriteNumberValue(Math.Round(lat, 5));
                        w.WriteEndArray();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(akis.ToArray());
    }

    /// <summary>Çözüm klasörünü (SecimSistemi.sln) bulur; harita dosyaları <c>SecimSistemi.Web/Harita</c> altına yazılır.</summary>
    public static string HaritaKlasoru()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SecimSistemi.sln")))
                return Path.Combine(d.FullName, "SecimSistemi.Web", "Harita");
        throw new YuklemeHatasi("SecimSistemi.sln bulunamadı; komutu depo klasöründe çalıştırın.");
    }

    /// <summary>
    /// <c>harita-sinirlari &lt;ilçe&gt; [--dosya yanit.json]</c>: indirir (ya da kaydedilmiş Overpass yanıtını okur), birleştirir,
    /// kaydeder, özet yazar.
    /// </summary>
    public static async Task<int> CalistirAsync(string ilceAdi, string? yanitDosyasi = null)
    {
        if (!BolgeTanimlari.Ilceler.TryGetValue(ilceAdi, out var ilce))
            throw new YuklemeHatasi($"'{ilceAdi}' tanımlı bir ilçe değil. Tanımlılar: {string.Join(", ", BolgeTanimlari.Ilceler.Keys)}.");

        string json;
        if (yanitDosyasi is not null)
        {
            Console.WriteLine($"{ilce.Il.Ad} / {ilce.Ad}: kaydedilmiş Overpass yanıtı okunuyor ({yanitDosyasi})…");
            json = await File.ReadAllTextAsync(yanitDosyasi);
        }
        else
        {
            Console.WriteLine($"{ilce.Il.Ad} / {ilce.Ad}: OpenStreetMap mahalle sınırları indiriliyor…");
            json = await IndirAsync(Sorgu(ilce));
        }
        var (mahalleler, uyarilar) = Coz(json);
        if (mahalleler.Count == 0)
            throw new YuklemeHatasi($"OSM'de '{OsmAdi(ilce.Ad)}' ilçesinin içinde mahalle sınırı (admin_level=8) bulunamadı.");

        var klasor = HaritaKlasoru();
        Directory.CreateDirectory(klasor);
        var yol = Path.Combine(klasor, $"{ilce.DosyaAdi}.geojson");
        await File.WriteAllTextAsync(yol, GeoJson(ilce, mahalleler, DateTime.Today));
        foreach (var u in uyarilar) Console.WriteLine($"UYARI: {u}");
        Console.WriteLine($"{mahalleler.Count} mahalle → {Path.GetRelativePath(Directory.GetCurrentDirectory(), yol)} ({new FileInfo(yol).Length / 1024} KB)");
        Console.WriteLine(Atif);
        Console.WriteLine("YSK adlarıyla eşleşmeyenler harita sayfasında listelenir (gerekirse Tanimlar/harita-eslemeleri.csv).");
        return 0;
    }
}
