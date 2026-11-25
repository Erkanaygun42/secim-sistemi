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
