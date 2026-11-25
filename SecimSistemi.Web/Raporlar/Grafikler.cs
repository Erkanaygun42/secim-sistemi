using System.Globalization;
using System.Security;
using System.Text;

namespace SecimSistemi.Web.Raporlar;

/// <summary>Grafik modeli: sayfada ve PDF'te aynı SVG'ye çizilir (<see cref="SvgCizici"/>). JavaScript gerekmez.</summary>
public abstract record Grafik(string Baslik, string? Aciklama);

/// <param name="Renk">Seri renginin sırası (<see cref="Gorunum.SeriRengi"/>); seriyi izler, grafikteki sırasını değil.</param>
/// <param name="Degerler"><see cref="CizgiGrafigi.Yillar"/> ile aynı sırada; boş değer (DSP pusulada değil, birim yok) çizgide boşluktur.</param>
public sealed record GrafikSerisi(string Ad, int Renk, IReadOnlyList<decimal?> Degerler);

/// <summary>Yıllara göre oran (yüzde) çizgisi.</summary>
/// <param name="DonemBaslangici">Verilirse bu yıldan önceki yıllar gri bantla "dönem dışı" işaretlenir ve çizgi bu sınırda kesilir.</param>
public sealed record CizgiGrafigi(string Baslik, string? Aciklama, IReadOnlyList<int> Yillar, IReadOnlyList<GrafikSerisi> Seriler,
    int? DonemBaslangici = null) : Grafik(Baslik, Aciklama);

/// <param name="Ipucu">Üzerine gelince görünen ek bilgi.</param>
/// <param name="Uyari">Sınır uyarısı olan birim (etiketin başında ⚠).</param>
public sealed record Cubuk(string Etiket, decimal Deger, string? Ipucu = null, bool Uyari = false);

/// <summary>Yatay çubuk grafik (büyükten küçüğe verilen sırayla).</summary>
/// <param name="Birim">Değerin yanına yazılan birim ("oy").</param>
public sealed record CubukGrafigi(string Baslik, string? Aciklama, string Birim, IReadOnlyList<Cubuk> Cubuklar) : Grafik(Baslik, Aciklama);

/// <summary>Noktalı grafiğin bir noktası.</summary>
/// <param name="Seri">Göstergedeki seri sırası (renk ve işaret biçimi).</param>
/// <param name="Etiketli">Grafikte adı yazılsın mı (uç değerler; diğerlerinin adı ipucunda ve tablodadır).</param>
public sealed record DagilimNoktasi(string Etiket, decimal X, decimal Y, int Seri, string? Ipucu = null, bool Etiketli = false);

/// <summary>Noktalı grafik, sıfır çizgileriyle dört kareye bölünmüş (2x2). Eksenler puan; sıfır her zaman görünür.</summary>
/// <param name="Seriler">Gösterge: seri adı ve renk sırası (<see cref="Gorunum.SeriRengi"/>).</param>
/// <param name="KareAdlari">Sağ üst, sol üst, sol alt, sağ alt.</param>
public sealed record DagilimGrafigi(string Baslik, string? Aciklama, string XAdi, string YAdi,
    IReadOnlyList<(string Ad, int Renk)> Seriler, IReadOnlyList<DagilimNoktasi> Noktalar, IReadOnlyList<string> KareAdlari)
    : Grafik(Baslik, Aciklama);

/// <summary>
/// Grafikleri SVG metnine çevirir. Kurallar: tek eksen, ince çizgiler (2 px), en az 8 px işaretler, silik ızgara,
/// değer etiketi çizginin sonunda / çubuğun ucunda, iki ve daha çok seride gösterge. Seriler renkten başka işaret
/// biçimiyle de ayrılır (daire, kare, eşkenar dörtgen, üçgen): siyah-beyaz baskıda ve renk körlüğünde de okunur.
/// Yazılar hiçbir zaman seri renginde değildir.
/// </summary>
public static class SvgCizici
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Yazi = "#333333", IkinciYazi = "#666666", Izgara = "#e3e3e3", Zemin = "#ffffff";

    public static string Ciz(Grafik g, Gorunum gorunum) => g switch
    {
        CizgiGrafigi c => Cizgi(c, gorunum),
        CubukGrafigi c => Cubuklar(c, gorunum),
        DagilimGrafigi d => Dagilim(d, gorunum),
        _ => throw new ArgumentOutOfRangeException(nameof(g)),
    };

    private static string F(double v) => v.ToString("0.#", Inv);
    private static string K(string s) => SecurityElement.Escape(s);

    /// <summary>Yüzde; ondalık sayısı adımın ondalığı kadar (adım 0,25 → %0,75; adım 0,5 → %1,5; adım 1 → %2).</summary>
    private static string Yuzde(decimal v, decimal adim)
    {
        var ondalik = 0;
        for (var a = adim; a != decimal.Truncate(a) && ondalik < 4; a *= 10) ondalik++;
        return "%" + v.ToString("N" + ondalik, Metinler.Turkce);
    }

    /// <summary>Eksen için "yuvarlak" adım: en büyük değer 4–5 adımda kapanır.</summary>
    public static decimal Adim(decimal enBuyuk)
    {
        if (enBuyuk <= 0) return 0.25m;
        decimal[] carpanlar = [1, 2, 2.5m, 5];
        for (var us = -3; us <= 4; us++)
            foreach (var c in carpanlar)
            {
                var adim = c * (decimal)Math.Pow(10, us);
                if (enBuyuk / adim <= 5) return adim;
            }
        return enBuyuk / 5;
    }

    // ---------------- Çizgi ----------------

    private static string Cizgi(CizgiGrafigi g, Gorunum gorunum)
    {
        const double W = 760, H = 290, sol = 52, sag = 70, ust = 40, alt = 30;
        var n = g.Yillar.Count;
        var enBuyuk = g.Seriler.SelectMany(s => s.Degerler).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty(0).Max();
        var adim = Adim(enBuyuk * 1.05m);
        var tavan = Math.Max(adim, Math.Ceiling(enBuyuk * 1.05m / adim) * adim);

        double X(int i) => n <= 1 ? (sol + W - sag) / 2 : sol + i * (W - sol - sag) / (n - 1);
        double Y(decimal v) => ust + (double)(1 - v / tavan) * (H - ust - alt);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg class=\"grafik\" viewBox=\"0 0 {F(W)} {F(H)}\" width=\"100%\" role=\"img\" aria-label=\"{K(g.Baslik)}\" ");
        sb.Append(CultureInfo.InvariantCulture, $"style=\"max-width:{F(W)}px;display:block;font-family:inherit;font-size:11px\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append(CultureInfo.InvariantCulture, $"<title>{K(g.Baslik)}</title>");

        // Dönem dışı bant
        var donemIndeksi = g.DonemBaslangici is { } db ? g.Yillar.ToList().FindIndex(y => y >= db) : -1;
        if (donemIndeksi > 0)
        {
            var x1 = (X(donemIndeksi - 1) + X(donemIndeksi)) / 2;
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(sol - 14)}\" y=\"{F(ust - 6)}\" width=\"{F(x1 - sol + 14)}\" height=\"{F(H - ust - alt + 6)}\" fill=\"#f0f0f0\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(sol + 6)}\" y=\"{F(ust + 8)}\" fill=\"{IkinciYazi}\" font-size=\"10\">karşılaştırılabilir dönem dışı</text>");
        }

        // Izgara ve y ekseni
        for (var v = 0m; v <= tavan; v += adim)
        {
            var y = Y(v);
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(sol)}\" x2=\"{F(W - sag)}\" y1=\"{F(y)}\" y2=\"{F(y)}\" stroke=\"{Izgara}\" stroke-width=\"1\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(sol - 8)}\" y=\"{F(y + 4)}\" text-anchor=\"end\" fill=\"{IkinciYazi}\">{K(Yuzde(v, adim))}</text>");
        }
        for (var i = 0; i < n; i++)
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(X(i))}\" y=\"{F(H - 10)}\" text-anchor=\"middle\" fill=\"{IkinciYazi}\">{g.Yillar[i]}</text>");

        // Gösterge (iki ve daha çok seride)
        if (g.Seriler.Count > 1)
        {
            var gx = sol;
            foreach (var s in g.Seriler)
            {
                var renk = gorunum.SeriRengi(s.Renk);
                sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(gx)}\" x2=\"{F(gx + 22)}\" y1=\"14\" y2=\"14\" stroke=\"{renk}\" stroke-width=\"2\"/>");
                sb.Append(Isaret(s.Renk, gx + 11, 14, renk, 1));
                var ad = s.Ad + (s.Degerler.All(v => v is null) ? " (pusulada değil)" : "");
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(gx + 28)}\" y=\"18\" fill=\"{Yazi}\">{K(ad)}</text>");
                gx += 40 + ad.Length * 6.2;
            }
        }

        // Seriler: çizgi parçaları (boş değerde ve dönem sınırında kesilir), işaretler, uçtaki değer
        var sonEtiketler = new List<(double X, double Y, string Metin)>();
        foreach (var s in g.Seriler)
        {
            var renk = gorunum.SeriRengi(s.Renk);
            var parca = new List<(double, double)>();
            void Bitir()
            {
                if (parca.Count > 1)
                    sb.Append(CultureInfo.InvariantCulture, $"<polyline points=\"{string.Join(' ', parca.Select(p => $"{F(p.Item1)},{F(p.Item2)}"))}\" fill=\"none\" stroke=\"{renk}\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\" opacity=\"{(donemIndeksi > 0 && parca[^1].Item1 < X(donemIndeksi) ? "0.45" : "1")}\"/>");
                parca.Clear();
            }
            for (var i = 0; i < n; i++)
            {
                if (i == donemIndeksi) Bitir();
                if (s.Degerler[i] is { } v) parca.Add((X(i), Y(v)));
                else Bitir();
            }
            Bitir();

            for (var i = 0; i < n; i++)
            {
                if (s.Degerler[i] is not { } v) continue;
                var disarda = donemIndeksi > 0 && i < donemIndeksi;
                var ipucu = $"{s.Ad} · {g.Yillar[i]}: {Yuzde(v, 0.01m)}" + (disarda ? " (karşılaştırılabilir dönem dışı)" : "");
                sb.Append(CultureInfo.InvariantCulture, $"<g><title>{K(ipucu)}</title><circle cx=\"{F(X(i))}\" cy=\"{F(Y(v))}\" r=\"10\" fill=\"transparent\"/>");
                sb.Append(Isaret(s.Renk, X(i), Y(v), renk, disarda ? 0.45 : 1));
                sb.Append("</g>");
            }

            var son = Enumerable.Range(0, n).LastOrDefault(i => s.Degerler[i] is not null, -1);
            if (son >= 0) sonEtiketler.Add((X(son), Y(s.Degerler[son]!.Value), Yuzde(s.Degerler[son]!.Value, 0.01m)));
        }

        // Uçtaki değerler: aynı yıldaki etiketler üst üste binmesin diye en az 13 px aralıklı dizilir;
        // dizi eksenin altına taşarsa bütünüyle yukarı kaydırılır (yıl etiketine binmesin).
        foreach (var grup in sonEtiketler.GroupBy(e => e.X))
        {
            var sirali = grup.OrderBy(e => e.Y).ToList();
            var yler = new List<double>();
            foreach (var e in sirali) yler.Add(Math.Max(e.Y + 4, yler.Count == 0 ? double.MinValue : yler[^1] + 13));
            var tasma = yler[^1] - (H - alt);
            for (var i = 0; i < sirali.Count; i++)
            {
                var y = tasma > 0 ? yler[i] - tasma : yler[i];
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(sirali[i].X + 9)}\" y=\"{F(y)}\" fill=\"{Yazi}\" font-weight=\"600\">{K(sirali[i].Metin)}</text>");
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    // ---------------- Noktalı (2x2) ----------------

    private static string Puan(decimal v, decimal adim)
    {
        var ondalik = 0;
        for (var a = adim; a != decimal.Truncate(a) && ondalik < 4; a *= 10) ondalik++;
        return (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("N" + ondalik, Metinler.Turkce);
    }

    /// <summary>Eksen aralığı: sıfırı içerir, uçlarda yuvarlak adım.</summary>
    public static (decimal Alt, decimal Ust, decimal Adim) Aralik(IEnumerable<decimal> degerler)
    {
        var l = degerler.DefaultIfEmpty(0).ToList();
        decimal enAz = Math.Min(0, l.Min()), enCok = Math.Max(0, l.Max());
        var adim = Adim(Math.Max(enCok - enAz, 0.01m) * 1.1m / 1.25m);
        var alt = Math.Floor(enAz / adim) * adim;
        var ust = Math.Ceiling(enCok / adim) * adim;
        if (alt == enAz && enAz < 0) alt -= adim;
        if (ust == enCok && enCok > 0) ust += adim;
        if (alt == 0 && ust == 0) ust = adim;
        return (alt, ust, adim);
    }

    private static string Dagilim(DagilimGrafigi g, Gorunum gorunum)
    {
        const double W = 760, H = 440, sol = 64, sag = 24, ust = 40, alt = 50;
        var (xa, xu, xs) = Aralik(g.Noktalar.Select(n => n.X));
        var (ya, yu, ys) = Aralik(g.Noktalar.Select(n => n.Y));
        double X(decimal v) => sol + (double)((v - xa) / (xu - xa)) * (W - sol - sag);
        double Y(decimal v) => ust + (double)((yu - v) / (yu - ya)) * (H - ust - alt);
        int RenkSirasi(int seri) => seri < g.Seriler.Count ? g.Seriler[seri].Renk : seri;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg class=\"grafik\" viewBox=\"0 0 {F(W)} {F(H)}\" width=\"100%\" role=\"img\" aria-label=\"{K(g.Baslik)}\" ");
        sb.Append(CultureInfo.InvariantCulture, $"style=\"max-width:{F(W)}px;display:block;font-family:inherit;font-size:11px\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append(CultureInfo.InvariantCulture, $"<title>{K(g.Baslik)}</title>");

        for (var v = xa; v <= xu; v += xs)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(X(v))}\" x2=\"{F(X(v))}\" y1=\"{F(ust)}\" y2=\"{F(H - alt)}\" stroke=\"{Izgara}\" stroke-width=\"1\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(X(v))}\" y=\"{F(H - alt + 14)}\" text-anchor=\"middle\" fill=\"{IkinciYazi}\">{K(Puan(v, xs))}</text>");
        }
        for (var v = ya; v <= yu; v += ys)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(sol)}\" x2=\"{F(W - sag)}\" y1=\"{F(Y(v))}\" y2=\"{F(Y(v))}\" stroke=\"{Izgara}\" stroke-width=\"1\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(sol - 6)}\" y=\"{F(Y(v) + 4)}\" text-anchor=\"end\" fill=\"{IkinciYazi}\">{K(Puan(v, ys))}</text>");
        }
        // Sıfır çizgileri dört kareyi ayırır.
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(X(0))}\" x2=\"{F(X(0))}\" y1=\"{F(ust)}\" y2=\"{F(H - alt)}\" stroke=\"#8a8a8a\" stroke-width=\"1\"/>");
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(sol)}\" x2=\"{F(W - sag)}\" y1=\"{F(Y(0))}\" y2=\"{F(Y(0))}\" stroke=\"#8a8a8a\" stroke-width=\"1\"/>");

        if (g.KareAdlari.Count == 4)
        {
            (double x, double y, string yon)[] yer =
            [
                (W - sag - 4, ust + 12, "end"), (sol + 4, ust + 12, "start"), (sol + 4, H - alt - 6, "start"), (W - sag - 4, H - alt - 6, "end"),
            ];
            for (var i = 0; i < 4; i++)
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(yer[i].x)}\" y=\"{F(yer[i].y)}\" text-anchor=\"{yer[i].yon}\" fill=\"{IkinciYazi}\" font-weight=\"600\">{K(g.KareAdlari[i])}</text>");
        }

        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F((sol + W - sag) / 2)}\" y=\"{F(H - 10)}\" text-anchor=\"middle\" fill=\"{Yazi}\">{K(g.XAdi)}</text>");
        sb.Append(CultureInfo.InvariantCulture, $"<text transform=\"translate(14 {F((ust + H - alt) / 2)}) rotate(-90)\" text-anchor=\"middle\" fill=\"{Yazi}\">{K(g.YAdi)}</text>");

        if (g.Seriler.Count > 1)
        {
            var gx = sol;
            foreach (var (ad, renk) in g.Seriler)
            {
                sb.Append(Isaret(renk, gx + 6, 14, gorunum.SeriRengi(renk), 1));
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(gx + 16)}\" y=\"18\" fill=\"{Yazi}\">{K(ad)}</text>");
                gx += 30 + ad.Length * 6.2;
            }
        }

        // Ad etiketleri: üst üste binecek olan yazılmaz (adı ipucunda ve tabloda kalır).
        var etiketKutulari = new List<(double X1, double Y1, double X2, double Y2)>();
        var etiketler = new StringBuilder();
        bool YerVar(double x1, double y1, double x2, double y2) =>
            etiketKutulari.All(k => x2 < k.X1 || x1 > k.X2 || y2 < k.Y1 || y1 > k.Y2);
        var xAd = g.XAdi.Split(':')[0];
        var yAd = g.YAdi.Split(':')[0];
        foreach (var n in g.Noktalar)
        {
            var renk = RenkSirasi(n.Seri);
            var ipucu = $"{n.Etiket}: {yAd} {Puan(n.Y, 0.01m)}, {xAd} {Puan(n.X, 0.01m)}" + (n.Ipucu is null ? "" : $" · {n.Ipucu}");
            sb.Append(CultureInfo.InvariantCulture, $"<g><title>{K(ipucu)}</title><circle cx=\"{F(X(n.X))}\" cy=\"{F(Y(n.Y))}\" r=\"10\" fill=\"transparent\"/>");
            sb.Append(Isaret(renk, X(n.X), Y(n.Y), gorunum.SeriRengi(renk), 0.9));
            var genislik = n.Etiket.Length * 6.0;
            var sagda = X(n.X) < W - sag - 170;
            var (kx1, kx2) = sagda ? (X(n.X) + 9, X(n.X) + 9 + genislik) : (X(n.X) - 9 - genislik, X(n.X) - 9);
            if (n.Etiketli && YerVar(kx1, Y(n.Y) - 7, kx2, Y(n.Y) + 5))
            {
                etiketKutulari.Add((kx1, Y(n.Y) - 7, kx2, Y(n.Y) + 5));
                // Etiket noktalardan sonra, beyaz kenarlıkla çizilir: başka bir noktanın üstüne düşse de okunur.
                etiketler.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(X(n.X) + (sagda ? 9 : -9))}\" y=\"{F(Y(n.Y) + 4)}\" text-anchor=\"{(sagda ? "start" : "end")}\" fill=\"{Yazi}\" stroke=\"{Zemin}\" stroke-width=\"3\" paint-order=\"stroke\">{K(n.Etiket)}</text>");
            }
            sb.Append("</g>");
        }
        sb.Append(etiketler);

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Serinin işareti: 0 daire, 1 kare, 2 eşkenar dörtgen, 3 üçgen; zemin renginde 2 px halka.</summary>
    private static string Isaret(int sira, double x, double y, string renk, double opaklik)
    {
        var ortak = $"fill=\"{renk}\" stroke=\"{Zemin}\" stroke-width=\"2\" opacity=\"{F(opaklik)}\"";
        return (sira % 4) switch
        {
            0 => $"<circle cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"5\" {ortak}/>",
            1 => $"<rect x=\"{F(x - 4.5)}\" y=\"{F(y - 4.5)}\" width=\"9\" height=\"9\" {ortak}/>",
            2 => $"<polygon points=\"{F(x)},{F(y - 6)} {F(x + 6)},{F(y)} {F(x)},{F(y + 6)} {F(x - 6)},{F(y)}\" {ortak}/>",
            _ => $"<polygon points=\"{F(x)},{F(y - 6)} {F(x + 6)},{F(y + 5)} {F(x - 6)},{F(y + 5)}\" {ortak}/>",
        };
    }

    // ---------------- Çubuk ----------------

    private static string Cubuklar(CubukGrafigi g, Gorunum gorunum)
    {
        const double W = 760, solEtiket = 280, sag = 90, satir = 26, kalinlik = 16, ust = 6, alt = 6;
        var H = ust + Math.Max(1, g.Cubuklar.Count) * satir + alt;
        var enBuyuk = g.Cubuklar.Select(c => c.Deger).DefaultIfEmpty(0).Max();
        double Uzunluk(decimal v) => enBuyuk <= 0 ? 0 : (double)(v / enBuyuk) * (W - solEtiket - sag);
        var renk = gorunum.AnaRenk;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg class=\"grafik\" viewBox=\"0 0 {F(W)} {F(H)}\" width=\"100%\" role=\"img\" aria-label=\"{K(g.Baslik)}\" ");
        sb.Append(CultureInfo.InvariantCulture, $"style=\"max-width:{F(W)}px;display:block;font-family:inherit;font-size:11px\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append(CultureInfo.InvariantCulture, $"<title>{K(g.Baslik)}</title>");
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(solEtiket)}\" x2=\"{F(solEtiket)}\" y1=\"{F(ust)}\" y2=\"{F(H - alt)}\" stroke=\"#bbbbbb\" stroke-width=\"1\"/>");

        for (var i = 0; i < g.Cubuklar.Count; i++)
        {
            var c = g.Cubuklar[i];
            var y = ust + i * satir + (satir - kalinlik) / 2;
            var u = Math.Max(Uzunluk(c.Deger), 1);
            var deger = $"{Metinler.Oy(c.Deger)} {g.Birim}";
            var ipucu = $"{c.Etiket}: {deger}" + (c.Ipucu is null ? "" : $" · {c.Ipucu}");
            var etiket = (c.Uyari ? "⚠ " : "") + c.Etiket;
            var r = Math.Min(4, u / 2);
            // Uç yuvarlak, taban düz: M taban → uç köşelerinde r yarıçap.
            var yol = $"M{F(solEtiket)},{F(y)} h{F(u - r)} a{F(r)},{F(r)} 0 0 1 {F(r)},{F(r)} v{F(kalinlik - 2 * r)} a{F(r)},{F(r)} 0 0 1 {F(-r)},{F(r)} h{F(-(u - r))} z";
            sb.Append(CultureInfo.InvariantCulture, $"<g><title>{K(ipucu)}</title>");
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"0\" y=\"{F(ust + i * satir)}\" width=\"{F(W)}\" height=\"{F(satir)}\" fill=\"transparent\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(solEtiket - 8)}\" y=\"{F(y + kalinlik / 2 + 4)}\" text-anchor=\"end\" fill=\"{Yazi}\">{K(etiket)}</text>");
            sb.Append(CultureInfo.InvariantCulture, $"<path d=\"{yol}\" fill=\"{renk}\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(solEtiket + u + 6)}\" y=\"{F(y + kalinlik / 2 + 4)}\" fill=\"{Yazi}\" font-weight=\"600\">{K(deger)}</text>");
            sb.Append("</g>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
