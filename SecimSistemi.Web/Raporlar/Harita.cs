using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;

namespace SecimSistemi.Web.Raporlar;

/// <summary>OSM'den alınmış bir mahalle sınırı; halkalar even-odd ile çizilir (iç halka delik olur).</summary>
public sealed record HaritaMahallesi(long OsmId, string Ad, IReadOnlyList<IReadOnlyList<(double Lon, double Lat)>> Halkalar);

/// <summary>Bir ilçenin harita dosyası (<c>SecimSistemi.Web/Harita/&lt;ilçe&gt;.geojson</c>, <c>harita-sinirlari</c> komutuyla).</summary>
public sealed record IlceHaritasi(string IlceKod, string Lisans, string Indirme, IReadOnlyList<HaritaMahallesi> Mahalleler)
{
    public static IlceHaritasi Oku(string json)
    {
        using var b = JsonDocument.Parse(json);
        var k = b.RootElement;
        var mahalleler = k.GetProperty("features").EnumerateArray().Select(f =>
        {
            var p = f.GetProperty("properties");
            var halkalar = f.GetProperty("geometry").GetProperty("coordinates").EnumerateArray()
                .Select(h => (IReadOnlyList<(double, double)>)h.EnumerateArray().Select(n => (n[0].GetDouble(), n[1].GetDouble())).ToList())
                .ToList();
            return new HaritaMahallesi(p.GetProperty("osm_id").GetInt64(), p.GetProperty("ad").GetString() ?? "", halkalar);
        }).ToList();
        return new IlceHaritasi(k.GetProperty("ilce").GetString()!, k.GetProperty("lisans").GetString()!, k.GetProperty("indirme").GetString()!, mahalleler);
    }
}

/// <summary>Elle eşleme: OSM mahallesi → YSK mahalle/köy kodu (<c>Tanimlar/harita-eslemeleri.csv</c>).</summary>
public sealed record HaritaEslemesi(string IlceKod, long OsmId, int BirimKod);

/// <summary>Uygulamanın yüklediği harita dosyaları ve elle eşlemeler.</summary>
public sealed class HaritaDeposu(IReadOnlyDictionary<string, IlceHaritasi> haritalar, IReadOnlyList<HaritaEslemesi> eslemeler)
{
    public const string EslemeDosyasi = "harita-eslemeleri.csv";

    public IlceHaritasi? Ilce(string kod) => haritalar.GetValueOrDefault(kod);

    public IReadOnlyList<HaritaEslemesi> Eslemeler(string kod) => eslemeler.Where(e => e.IlceKod == kod).ToList();

    /// <summary>Çıktı klasöründeki <c>Harita/*.geojson</c> dosyaları ve <c>Tanimlar/harita-eslemeleri.csv</c>.</summary>
    public static HaritaDeposu Yukle()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "Harita");
        var haritalar = Directory.Exists(klasor)
            ? Directory.GetFiles(klasor, "*.geojson").Select(f => IlceHaritasi.Oku(File.ReadAllText(f))).ToDictionary(h => h.IlceKod)
            : [];
        var csv = Path.Combine(AppContext.BaseDirectory, "Tanimlar", EslemeDosyasi);
        return new HaritaDeposu(haritalar, File.Exists(csv) ? EslemeleriCoz(File.ReadAllLines(csv)) : []);
    }

    /// <summary>Biçim: <c>ilce;osm_id;birim_kod;not</c>; # ile başlayan satır yorumdur.</summary>
    public static List<HaritaEslemesi> EslemeleriCoz(IEnumerable<string> satirlar)
    {
        var sonuc = new List<HaritaEslemesi>();
        var no = 0;
        foreach (var ham in satirlar)
        {
            no++;
            var s = ham.Trim();
            if (s.Length == 0 || s.StartsWith('#') || s.StartsWith("ilce;", StringComparison.Ordinal)) continue;
            var p = s.Split(';');
            if (p.Length < 3 || !long.TryParse(p[1], out var osm) || !int.TryParse(p[2], out var kod))
                throw new FormatException($"{EslemeDosyasi}:{no}: 'ilce;osm_id;birim_kod;not' biçiminde olmalı.");
            sonuc.Add(new HaritaEslemesi(p[0].Trim(), osm, kod));
        }
        return sonuc;
    }
}

/// <summary>Haritadaki mahalleler ile YSK mahalleleri arasındaki eşleşme.</summary>
/// <param name="OsmdenBirime">OSM kimliği → YSK mahalle/köy kodu.</param>
/// <param name="EslesmeyenOsm">YSK'da karşılığı bulunamayan OSM mahalleleri.</param>
/// <param name="EslesmeyenYsk">Haritada karşılığı bulunamayan YSK mahalleleri (kod, ad).</param>
public sealed record HaritaEslesmesi(IReadOnlyDictionary<long, int> OsmdenBirime, IReadOnlyList<HaritaMahallesi> EslesmeyenOsm,
    IReadOnlyList<(int Kod, string Ad)> EslesmeyenYsk);

/// <summary>Bir sınıfın alt ve üst sınırı ile rengi.</summary>
public sealed record RenkSinifi(decimal Alt, decimal Ust, string Renk);

/// <summary>
/// Mahalle haritası (docs/kararlar.md K45): OSM sınırları sunucuda SVG'ye çizilir; JavaScript ve arka plan haritası yoktur,
/// internetsiz çalışır. Mahalleler DSP oranına göre tek renkli (ana renk) beş tonla boyanır; sınıflar beşte birlik dilimlerdir
/// (her tonda yaklaşık aynı sayıda mahalle). Veri yoksa gri, YSK'da eşleşmeyen mahalle taralı gri.
/// </summary>
public static class HaritaCizici
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Karşılaştırma için ad: büyük harf, "MAHALLESİ"/"MAH." eki ve noktalama atılır.</summary>
    public static string Normal(string ad)
    {
        var b = ad.ToUpper(Tr).Replace('.', ' ').Replace(',', ' ');
        var sozcukler = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        sozcukler.RemoveAll(s => s is "MAHALLESİ" or "MAH" or "MAHALLE");
        return string.Join(' ', sozcukler);
    }

    /// <summary>
    /// Önce elle eşlemeler, sonra ad tam eşleşmesi, sonra OSM adının YSK adıyla başlaması (tek aday varsa;
    /// "Akevler Mahallesi 7 sokak" gibi hatalı OSM adları için).
    /// </summary>
    public static HaritaEslesmesi Eslestir(IlceHaritasi harita, IReadOnlyList<(int Kod, string Ad)> birimler, IReadOnlyList<HaritaEslemesi> elle)
    {
        var sonuc = new Dictionary<long, int>();
        var bosBirim = birimler.ToDictionary(b => b.Kod);
        foreach (var e in elle.Where(e => bosBirim.ContainsKey(e.BirimKod) && harita.Mahalleler.Any(m => m.OsmId == e.OsmId)))
        {
            sonuc[e.OsmId] = e.BirimKod;
            bosBirim.Remove(e.BirimKod);
        }

        foreach (var m in harita.Mahalleler.Where(m => !sonuc.ContainsKey(m.OsmId)))
        {
            var ad = Normal(m.Ad);
            var adaylar = bosBirim.Values.Where(b => Normal(b.Ad) == ad).ToList();
            if (adaylar.Count != 1)
                adaylar = bosBirim.Values.Where(b => ad.StartsWith(Normal(b.Ad) + " ", StringComparison.Ordinal)).ToList();
            if (adaylar.Count == 1)
            {
                sonuc[m.OsmId] = adaylar[0].Kod;
                bosBirim.Remove(adaylar[0].Kod);
            }
        }

        return new HaritaEslesmesi(sonuc,
            harita.Mahalleler.Where(m => !sonuc.ContainsKey(m.OsmId)).ToList(),
            bosBirim.Values.OrderBy(b => b.Ad, StringComparer.Create(Tr, true)).ToList());
    }

    /// <summary>Beş tonluk sınıflar (beşte birlik dilimler); farklı değer azsa daha az sınıf.</summary>
    public static IReadOnlyList<RenkSinifi> Siniflar(IEnumerable<decimal> degerler, Gorunum gorunum, int adet = 5)
    {
        var l = degerler.Order().ToList();
        if (l.Count == 0) return [];
        var sinirlar = Enumerable.Range(0, adet + 1).Select(i => l[Math.Min(l.Count - 1, (int)Math.Round(i * (l.Count - 1) / (double)adet))]).Distinct().ToList();
        if (sinirlar.Count == 1) return [new RenkSinifi(sinirlar[0], sinirlar[0], Ton(gorunum, 1))];
        var n = sinirlar.Count - 1;
        return [.. Enumerable.Range(0, n).Select(i => new RenkSinifi(sinirlar[i], sinirlar[i + 1], Ton(gorunum, n == 1 ? 1 : (i + 1) / (double)n)))];
    }

    public static string? Renk(IReadOnlyList<RenkSinifi> siniflar, decimal? deger) =>
        deger is not { } d || siniflar.Count == 0 ? null : (siniflar.FirstOrDefault(s => d <= s.Ust) ?? siniflar[^1]).Renk;

    /// <summary>Açık tondan ana rengin koyu tonuna (t: 0–1); 0'da çok açık mavi.</summary>
    public static string Ton(Gorunum g, double t)
    {
        static (int, int, int) C(string h) => (Convert.ToInt32(h[1..3], 16), Convert.ToInt32(h[3..5], 16), Convert.ToInt32(h[5..7], 16));
        var (r0, g0, b0) = (234, 246, 252);
        var (r1, g1, b1) = C(g.Koyu);
        var k = 0.15 + 0.85 * t;
        return $"#{(int)(r0 + (r1 - r0) * k):x2}{(int)(g0 + (g1 - g0) * k):x2}{(int)(b0 + (b1 - b0) * k):x2}";
    }

    public const string DonemDisiRengi = "#e6e6e6";

    /// <param name="deger">OSM kimliği → (değer, ipucu, dönem dışı mı); eşleşmeyen mahalle için boş.</param>
    public static string Ciz(IlceHaritasi harita, Func<long, (decimal? Deger, string Ipucu, bool DonemDisi)?> deger, IReadOnlyList<RenkSinifi> siniflar)
    {
        const double W = 760;
        var noktalar = harita.Mahalleler.SelectMany(m => m.Halkalar.SelectMany(h => h)).ToList();
        if (noktalar.Count == 0) return "";
        double minLon = noktalar.Min(p => p.Lon), maxLon = noktalar.Max(p => p.Lon), minLat = noktalar.Min(p => p.Lat), maxLat = noktalar.Max(p => p.Lat);
        var kx = Math.Cos((minLat + maxLat) / 2 * Math.PI / 180); // boylamın enlemdeki kısalması (eşdikdörtgen izdüşüm)
        var olcek = (W - 20) / Math.Max((maxLon - minLon) * kx, 1e-9);
        var H = (maxLat - minLat) * olcek + 20;
        string P(double lon, double lat) => $"{((lon - minLon) * kx * olcek + 10).ToString("0.#", Inv)},{((maxLat - lat) * olcek + 10).ToString("0.#", Inv)}";

        var sb = new StringBuilder();
        sb.Append(Inv, $"<svg class=\"harita\" viewBox=\"0 0 {W.ToString(Inv)} {H.ToString("0", Inv)}\" width=\"100%\" role=\"img\" aria-label=\"Mahalle haritası\" ");
        sb.Append(Inv, $"style=\"max-width:{W.ToString(Inv)}px;max-height:78vh;display:block\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append("<defs><pattern id=\"tarama\" width=\"6\" height=\"6\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\">" +
                  "<rect width=\"6\" height=\"6\" fill=\"#eeeeee\"/><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"#bdbdbd\" stroke-width=\"2\"/></pattern></defs>");
        foreach (var m in harita.Mahalleler)
        {
            var d = deger(m.OsmId);
            var dolgu = d is null ? "url(#tarama)" : d.Value.DonemDisi ? DonemDisiRengi : Renk(siniflar, d.Value.Deger) ?? "#d9d9d9";
            var ipucu = d?.Ipucu ?? $"{m.Ad}: YSK mahallesiyle eşleşmedi";
            var yol = string.Concat(m.Halkalar.Select(h => "M" + string.Join("L", h.Select(p => P(p.Lon, p.Lat))) + "Z"));
            sb.Append($"<path d=\"{yol}\" fill=\"{dolgu}\" fill-rule=\"evenodd\" stroke=\"#ffffff\" stroke-width=\"1.2\"><title>{SecurityElement.Escape(ipucu)}</title></path>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }
}
