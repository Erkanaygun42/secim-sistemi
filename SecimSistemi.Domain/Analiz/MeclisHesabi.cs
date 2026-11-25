namespace SecimSistemi.Domain.Analiz;

/// <summary>Belediye meclisi seçiminde bir parti listesi ya da bağımsız aday ve aldığı geçerli oy.</summary>
public sealed record MeclisListesi(string Ad, long Oy, bool Bagimsiz = false);

/// <summary>Bir listenin sonucu.</summary>
/// <param name="KalanOy">Oy − geçerli oyun onda biri (2972 m.23/a); barajı geçemeyende 0 ya da eksi.</param>
/// <param name="Koltuk">Bölme (D'Hondt) ile kazanılan asıl üyelik.</param>
/// <param name="Kontenjan">Kontenjan üyeliği (yalnızca en çok oy alan partiye).</param>
public sealed record ListeSonucu(string Ad, long Oy, bool Bagimsiz, decimal Oran, bool BarajiGecti, decimal KalanOy, int Koltuk, int Kontenjan)
{
    public int Toplam => Koltuk + Kontenjan;
}

/// <param name="BarajOyu">Geçerli oyun onda biri; barajı geçmek için bundan fazla oy gerekir.</param>
/// <param name="KuraListeleri">Son koltuk(lar) eşit paylar yüzünden kurayla belirlenecekse eşit kalan listeler; boşsa kura yok.
/// Hesap bu durumda koltuğu toplam oyu fazla olana verir (kurayı öngöremez).</param>
/// <param name="SonPay">Bölmeyle verilen son koltuğun payı (kalan oy ÷ bölen); bir sonraki koltuğun eşiği için.</param>
public sealed record MeclisSonucu(int UyeSayisi, int KontenjanSayisi, long GecerliOy, decimal BarajOyu,
    IReadOnlyList<ListeSonucu> Listeler, string? KontenjanAlan, IReadOnlyList<string> KuraListeleri, decimal? SonPay)
{
    public ListeSonucu? Liste(string ad) => Listeler.FirstOrDefault(l => l.Ad == ad);

    public int Koltuk(string ad) => Liste(ad)?.Toplam ?? 0;
}

/// <summary>Ek oy nereden gelir (senaryo).</summary>
public enum EkOyKaynagi
{
    /// <summary>Oy vermeyen ya da geçersiz oy veren seçmenden: geçerli oy toplamı da artar.</summary>
    YeniSecmen,

    /// <summary>En çok oy alan diğer listeden: geçerli oy toplamı değişmez.</summary>
    EnBuyukParti,
}

/// <summary>"+X oyla kaç üye": DSP'nin o sayıda üyeye ulaşması için gereken en az ek oy.</summary>
public sealed record KoltukEsigi(int Koltuk, long EkOy);

/// <summary>
/// Belediye meclisi üye dağılımı, 2972 sayılı Kanun'a göre (metin mevzuat.gov.tr'den doğrulandı; docs/kararlar.md K36):
/// <list type="number">
/// <item>Geçerli oy toplamının onda biri her partinin ve bağımsız adayın oyundan ayrı ayrı çıkarılır; geriye oyu
///   kalmayan hesaba katılmaz (m.23/a). Bu yüzden barajı geçmek için %10'dan fazlası gerekir (tam %10'da kalan 0'dır).</item>
/// <item>Kalan oylar 1, 2, 3 ... diye bölünür; paylar parti ayrımı yapılmadan büyükten küçüğe sıralanır ve üyelikler bu
///   sırayla verilir (m.23/a). Bölmeye giren üye sayısından kontenjan sayısı çıkarılır (m.23/a, 3507 ek).</item>
/// <item>Son üyelikte eşit paylar varsa kura çekilir (m.23/a); hesap kurayı öngöremez, eşitliği bildirir.</item>
/// <item>Kontenjan üyeliklerinin hepsini geçerli oyların en çoğunu alan siyasi parti kazanır (m.23/a); sayısı üye sayısına
///   göredir (m.10/c).</item>
/// </list>
/// Varsayımlar (metinde açık değil): bağımsız aday tek kişidir, en çok bir üyelik alır; her parti kontenjan adayı göstermiştir;
/// kontenjanı alan "en çok oy alan siyasi parti" bağımsızlar arasından değil partiler arasından seçilir.
/// </summary>
public static class MeclisHesabi
{
    /// <summary>Nüfusa göre belediye meclisi üye sayısı (2972 m.5/b). Bilgi içindir: ilan edilen üye sayısı esas alınır.</summary>
    public static int UyeSayisi(long nufus) => nufus switch
    {
        <= 10_000 => 9,
        <= 20_000 => 11,
        <= 50_000 => 15,
        <= 100_000 => 25,
        <= 250_000 => 31,
        <= 500_000 => 37,
        <= 1_000_000 => 45,
        _ => 55,
    };

    /// <summary>Kontenjan üyeliği sayısı (2972 m.10/c).</summary>
    public static int KontenjanSayisi(int uyeSayisi) => uyeSayisi switch
    {
        9 or 11 => 1,
        15 => 2,
        25 or 31 => 3,
        37 => 4,
        45 => 5,
        55 => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(uyeSayisi), uyeSayisi,
            "2972 m.5/b'deki üye sayılarından biri olmalı (9, 11, 15, 25, 31, 37, 45, 55)."),
    };

    public static MeclisSonucu Dagit(IReadOnlyList<MeclisListesi> listeler, int uyeSayisi)
    {
        ArgumentNullException.ThrowIfNull(listeler);
        if (listeler.Select(l => l.Ad).Distinct().Count() != listeler.Count)
            throw new ArgumentException("Liste adları benzersiz olmalı.", nameof(listeler));
        if (listeler.Any(l => l.Oy < 0)) throw new ArgumentException("Oy negatif olamaz.", nameof(listeler));

        var kontenjan = KontenjanSayisi(uyeSayisi);
        var bolunecek = uyeSayisi - kontenjan;
        var gecerli = listeler.Sum(l => l.Oy);

        // Kalan oy tam sayıyla: 10 × (oy − geçerli/10) = 10 × oy − geçerli. Paylar çapraz çarpımla karşılaştırılır; yuvarlama yok.
        long Kalan10(MeclisListesi l) => 10 * l.Oy - gecerli;

        var paylar = new List<Pay>();
        foreach (var (l, sira) in listeler.Select((l, i) => (l, i)))
        {
            var k = Kalan10(l);
            if (k <= 0) continue;
            var adet = l.Bagimsiz ? 1 : bolunecek;
            for (var b = 1; b <= adet; b++) paylar.Add(new Pay(sira, k, b, l.Oy, l.Ad));
        }
        paylar.Sort(PayKarsilastirici.Instance);

        var verilen = paylar.Take(bolunecek).ToList();
        var koltuklar = new int[listeler.Count];
        foreach (var p in verilen) koltuklar[p.Liste]++;

        // Kura: verilen son pay ile dışarıda kalan ilk pay eşitse, o eşit paylara sahip listeler kuraya girer.
        var kura = new List<string>();
        if (verilen.Count == bolunecek && paylar.Count > bolunecek)
        {
            var son = verilen[^1];
            var esitler = paylar.Where(p => PayKarsilastirici.Esit(p, son)).ToList();
            if (esitler.Count > verilen.Count(p => PayKarsilastirici.Esit(p, son)))
                kura = esitler.Select(p => p.Ad).Distinct().ToList();
        }

        // Kontenjan: geçerli oyların en çoğunu alan siyasi parti (bağımsız değil). Eşitlik metinde yok: ad sırası (varsayım).
        var kontenjanAlan = listeler.Where(l => !l.Bagimsiz && l.Oy > 0)
            .OrderByDescending(l => l.Oy).ThenBy(l => l.Ad, StringComparer.Ordinal).FirstOrDefault()?.Ad;

        var sonuc = listeler.Select((l, i) => new ListeSonucu(l.Ad, l.Oy, l.Bagimsiz,
                gecerli == 0 ? 0 : 100m * l.Oy / gecerli, Kalan10(l) > 0, Kalan10(l) / 10m, koltuklar[i],
                l.Ad == kontenjanAlan ? kontenjan : 0))
            .OrderByDescending(s => s.Toplam).ThenByDescending(s => s.Oy).ThenBy(s => s.Ad, StringComparer.Ordinal)
            .ToList();

        decimal? sonPay = verilen.Count == 0 ? null : verilen[^1].Kalan10 / 10m / verilen[^1].Bolen;
        return new MeclisSonucu(uyeSayisi, kontenjan, gecerli, gecerli / 10m, sonuc, kontenjanAlan, kura, sonPay);
    }

    /// <summary>Hedef listeye <paramref name="ekOy"/> oy eklenmiş listeler.</summary>
    /// <exception cref="ArgumentException">Hedef yok; ya da ek oy en büyük partinin oyundan fazla.</exception>
    public static IReadOnlyList<MeclisListesi> EkOyEkle(IReadOnlyList<MeclisListesi> listeler, string hedef, long ekOy, EkOyKaynagi kaynak)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ekOy);
        if (listeler.All(l => l.Ad != hedef)) throw new ArgumentException($"'{hedef}' listelerde yok.", nameof(hedef));

        var kaynakAd = kaynak == EkOyKaynagi.EnBuyukParti ? EnBuyukDiger(listeler, hedef)?.Ad : null;
        var sonuc = listeler.Select(l =>
            l.Ad == hedef ? l with { Oy = l.Oy + ekOy } :
            l.Ad == kaynakAd ? l with { Oy = l.Oy - ekOy } :
            l).ToList();
        if (sonuc.Any(l => l.Oy < 0))
            throw new ArgumentException($"Ek oy ({ekOy}) en büyük partinin oyundan fazla.", nameof(ekOy));
        return sonuc;
    }

    /// <summary>En çok oy alan diğer liste (hedef dışında); oy eşitse ad sırası.</summary>
    public static MeclisListesi? EnBuyukDiger(IReadOnlyList<MeclisListesi> listeler, string hedef) =>
        listeler.Where(l => l.Ad != hedef).OrderByDescending(l => l.Oy).ThenBy(l => l.Ad, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Hedefin barajı geçmesi için gereken en az ek oy (0: zaten geçiyor); ulaşılamıyorsa boş.</summary>
    public static long? BarajIcinGerekenOy(IReadOnlyList<MeclisListesi> listeler, int uyeSayisi, string hedef, EkOyKaynagi kaynak) =>
        EnAzEkOy(listeler, hedef, kaynak, x => Dagit(EkOyEkle(listeler, hedef, x, kaynak), uyeSayisi).Liste(hedef)!.BarajiGecti);

    /// <summary>Hedefin en az <paramref name="koltuk"/> üyeye ulaşması için gereken en az ek oy; ulaşılamıyorsa boş.</summary>
    public static long? KoltukIcinGerekenOy(IReadOnlyList<MeclisListesi> listeler, int uyeSayisi, string hedef, int koltuk, EkOyKaynagi kaynak) =>
        EnAzEkOy(listeler, hedef, kaynak, x => Dagit(EkOyEkle(listeler, hedef, x, kaynak), uyeSayisi).Koltuk(hedef) >= koltuk);

    /// <summary>1'den <paramref name="enCokKoltuk"/>'a kadar her üye sayısı için gereken ek oy (ulaşılamayan koltukta durur).</summary>
    public static IReadOnlyList<KoltukEsigi> KoltukEsikleri(IReadOnlyList<MeclisListesi> listeler, int uyeSayisi, string hedef,
        EkOyKaynagi kaynak, int enCokKoltuk)
    {
        var esikler = new List<KoltukEsigi>();
        var mevcut = Dagit(listeler, uyeSayisi).Koltuk(hedef);
        for (var k = mevcut + 1; k <= enCokKoltuk; k++)
        {
            if (KoltukIcinGerekenOy(listeler, uyeSayisi, hedef, k, kaynak) is not { } x) break;
            esikler.Add(new KoltukEsigi(k, x));
        }
        return esikler;
    }

    /// <summary>
    /// Koşulu sağlayan en az ek oy (ikili arama). Hedefin üye sayısı ek oyla azalmaz: hedefin kalan oyu artar, diğerlerininki
    /// azalır ya da aynı kalır. Üst sınır: yeni seçmende geçerli oyun 100 katı, en büyük partiden alırken o partinin oyu.
    /// </summary>
    private static long? EnAzEkOy(IReadOnlyList<MeclisListesi> listeler, string hedef, EkOyKaynagi kaynak, Func<long, bool> kosul)
    {
        if (kosul(0)) return 0;
        var ust = kaynak == EkOyKaynagi.YeniSecmen
            ? Math.Max(1, listeler.Sum(l => l.Oy)) * 100
            : EnBuyukDiger(listeler, hedef)?.Oy ?? 0;
        if (ust == 0 || !kosul(ust)) return null;
        long alt = 0; // kosul(alt) yanlış, kosul(ust) doğru
        while (ust - alt > 1)
        {
            var orta = alt + (ust - alt) / 2;
            if (kosul(orta)) ust = orta; else alt = orta;
        }
        return ust;
    }

    /// <param name="Kalan10">10 × kalan oy (tam sayı).</param>
    private readonly record struct Pay(int Liste, long Kalan10, int Bolen, long Oy, string Ad);

    /// <summary>Büyük pay önce (Kalan10 ÷ Bölen, çapraz çarpımla); eşitlikte toplam oyu fazla olan, sonra ad (kurayı öngörmez).</summary>
    private sealed class PayKarsilastirici : IComparer<Pay>
    {
        public static readonly PayKarsilastirici Instance = new();

        public static bool Esit(Pay a, Pay b) => (Int128)a.Kalan10 * b.Bolen == (Int128)b.Kalan10 * a.Bolen;

        public int Compare(Pay a, Pay b)
        {
            var c = ((Int128)b.Kalan10 * a.Bolen).CompareTo((Int128)a.Kalan10 * b.Bolen);
            if (c != 0) return c;
            c = b.Oy.CompareTo(a.Oy);
            return c != 0 ? c : string.CompareOrdinal(a.Ad, b.Ad);
        }
    }
}
