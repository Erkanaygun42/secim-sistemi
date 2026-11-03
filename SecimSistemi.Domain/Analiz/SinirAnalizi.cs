namespace SecimSistemi.Domain.Analiz;

/// <summary>Bir karşılaştırma biriminin bir seçimdeki sınır durumu.</summary>
public enum SinirDurumu
{
    /// <summary>Birim o seçimde yoktu (sonradan kuruldu); değerleri boştur.</summary>
    SonradanKuruldu,

    /// <summary>Birim o seçimde ilk kez görünüyor (ilk seçim değil). Karşılaştırılabilir dönem burada başlar.</summary>
    Yeni,

    /// <summary>Sınır değişmemiş sayılır.</summary>
    Sabit,

    /// <summary>Aynı seçimde ilçede yeni mahalle var ve pay belirgin değişti, ama eşiğe ulaşmadı. Dönem kesilmez, raporda uyarılır.</summary>
    Supheli,

    /// <summary>Aynı seçimde ilçede yeni mahalle var ve pay eşikten fazla değişti. Karşılaştırılabilir dönem burada yeniden başlar.</summary>
    SiniriDegisti,
}

/// <summary>
/// Sınır değişikliği eşikleri; pay değişimi oran olarak (0,25 = %25). Esenyurt (tek ilçe) ile ayarlandı;
/// il geneline geçince onaylı insan kararlarıyla yeniden ayarlanacak (docs/kararlar.md).
/// </summary>
public sealed record SinirEsikleri(decimal Dusus, decimal Artis, decimal Supheli)
{
    public static readonly SinirEsikleri Varsayilan = new(Dusus: 0.25m, Artis: 0.60m, Supheli: 0.15m);

    public void Dogrula()
    {
        if (Dusus is <= 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(Dusus), Dusus, "Düşüş eşiği 0 ile 1 arasında olmalı.");
        if (Artis <= 0) throw new ArgumentOutOfRangeException(nameof(Artis), Artis, "Artış eşiği pozitif olmalı.");
        if (Supheli <= 0 || Supheli >= Math.Min(Dusus, Artis))
            throw new ArgumentOutOfRangeException(nameof(Supheli), Supheli, "Şüpheli eşiği pozitif ve düşüş/artış eşiklerinden küçük olmalı.");
    }
}

/// <summary>Bir birimin seçimlerdeki belediye meclisi toplam oyu (anahtar: yıl). Birim o yıl yoksa anahtar da yoktur.</summary>
/// <param name="EslemeyleKatilanYillar">Onaylı eşlemeyle birime katılan bir mahallenin ilk göründüğü yıllar.
/// Bu yıllarda eşleme insan kararıdır; birim otomatik tespite bakılmadan doğrulanmış sabit sayılır.</param>
public sealed record SinirBirimi(int BirimKod, string BirimAdi, IReadOnlyDictionary<int, long> MeclisOyu, IReadOnlySet<int> EslemeyleKatilanYillar);

/// <summary>Onaylanmış, geçerli insan kararı: birim o seçimde sabit mi, sınırı değişti mi.</summary>
public sealed record SinirKarari(int BirimKod, int Yil, SinirDurumu Karar, string Aciklama);

public sealed record SinirGirdisi(
    IReadOnlyList<int> Yillar,
    IReadOnlyList<SinirBirimi> Birimler,
    IReadOnlySet<int> YeniMahalleYillari,
    IReadOnlyList<SinirKarari> Kararlar);

/// <summary>Bir birimin bir seçimdeki durumu. <see cref="Sonuc"/>, insan kararı varsa otomatik tespitin yerine geçer.</summary>
/// <param name="Pay">Birimin ilçe meclis oyundaki payı (0–1); birim yoksa boş.</param>
/// <param name="PayDegisimi">Bir önceki seçime göre göreli değişim (−0,30 = %30 düştü); hesaplanamıyorsa boş.</param>
public sealed record BirimYilDurumu(int Yil, SinirDurumu Otomatik, SinirDurumu Sonuc, decimal? Pay, decimal? PayDegisimi, string? Karar)
{
    /// <summary>Sonuç bir insan kararından (onaylı durum kararı ya da eşleme) geliyor.</summary>
    public bool Dogrulandi => Karar is not null;
}

/// <summary>Bir birimin sınır geçmişi ve karşılaştırılabilir dönemi (son sınır değişikliğinden son seçime).</summary>
public sealed record BirimDonemi(int BirimKod, string BirimAdi, int DonemBaslangici, int DonemSonu, IReadOnlyList<BirimYilDurumu> Yillar)
{
    public bool SupheliVar => Yillar.Any(y => y.Sonuc == SinirDurumu.Supheli && y.Yil >= DonemBaslangici);
    public string Donem => DonemBaslangici == DonemSonu ? $"{DonemSonu}" : $"{DonemBaslangici}–{DonemSonu}";
}

public sealed record SinirAnaliziSonucu(SinirEsikleri Esikler, IReadOnlyList<int> Yillar, IReadOnlyList<BirimDonemi> Birimler)
{
    public BirimDonemi? Birim(int kod) => Birimler.FirstOrDefault(b => b.BirimKod == kod);
}

/// <summary>
/// Mahalle sınır değişikliklerinin otomatik tespiti. Referans, en son seçimde var olan birimlerdir.
/// Pay = birimin belediye meclisi oyu ÷ ilçenin meclis oyu. Bir seçimde (ilk seçim değil):
/// <list type="bullet">
/// <item>ilçede yeni mahalle yoksa: Sabit (pay değişimi büyüme ya da göçtür, sınır değişikliği değil);</item>
/// <item>yeni mahalle varsa ve pay en az <see cref="SinirEsikleri.Dusus"/> düştüyse ya da en az <see cref="SinirEsikleri.Artis"/> arttıysa: SiniriDegisti;</item>
/// <item>yeni mahalle varsa ve pay <see cref="SinirEsikleri.Supheli"/>'den fazla ama eşikten az değiştiyse: Supheli;</item>
/// <item>yoksa Sabit.</item>
/// </list>
/// İnsan kararları önceliklidir: onaylı eşlemeyle birime mahalle katılan seçimde birim Sabit, onaylı durum kararı
/// olan seçimde kararın kendisi geçerlidir. Karşılaştırılabilir dönem, son "Yeni" ya da "SiniriDegisti" seçiminde başlar;
/// Şüpheli dönemi kesmez.
/// </summary>
public static class SinirAnalizi
{
    public static SinirAnaliziSonucu Hesapla(SinirGirdisi girdi, SinirEsikleri esikler)
    {
        ArgumentNullException.ThrowIfNull(girdi);
        esikler.Dogrula();

        var yillar = girdi.Yillar.Order().ToList();
        if (yillar.Count == 0)
            return new SinirAnaliziSonucu(esikler, yillar, []);
        int ilkYil = yillar[0], sonYil = yillar[^1];

        var ilceToplami = yillar.ToDictionary(y => y, y => girdi.Birimler.Sum(b => b.MeclisOyu.GetValueOrDefault(y)));
        var kararlar = girdi.Kararlar.ToLookup(k => (k.BirimKod, k.Yil));

        var birimler = new List<BirimDonemi>();
        foreach (var b in girdi.Birimler.Where(b => b.MeclisOyu.ContainsKey(sonYil)).OrderBy(b => b.BirimKod))
        {
            var durumlar = new List<BirimYilDurumu>();
            decimal? oncekiPay = null;
            bool goruldu = false;

            foreach (var y in yillar)
            {
                decimal? pay = b.MeclisOyu.TryGetValue(y, out var oy) && ilceToplami[y] > 0 ? (decimal)oy / ilceToplami[y] : null;
                decimal? degisim = pay is not null && oncekiPay > 0 ? (pay - oncekiPay) / oncekiPay : null;

                SinirDurumu otomatik;
                if (!b.MeclisOyu.ContainsKey(y)) otomatik = SinirDurumu.SonradanKuruldu;
                else if (!goruldu) otomatik = y == ilkYil ? SinirDurumu.Sabit : SinirDurumu.Yeni;
                else otomatik = Otomatik(degisim, girdi.YeniMahalleYillari.Contains(y), esikler);

                var (sonuc, karar) = (otomatik, (string?)null);
                if (otomatik is not SinirDurumu.SonradanKuruldu and not SinirDurumu.Yeni)
                {
                    if (kararlar[(b.BirimKod, y)].LastOrDefault() is { } k)
                        (sonuc, karar) = (k.Karar, k.Aciklama);
                    else if (b.EslemeyleKatilanYillar.Contains(y))
                        (sonuc, karar) = (SinirDurumu.Sabit, "onaylı eşleme: birime katılan mahalle bu seçimde kuruldu");
                }

                durumlar.Add(new BirimYilDurumu(y, otomatik, sonuc, pay, degisim, karar));
                if (b.MeclisOyu.ContainsKey(y)) goruldu = true;
                oncekiPay = pay;
            }

            var baslangic = durumlar.LastOrDefault(d => d.Sonuc is SinirDurumu.Yeni or SinirDurumu.SiniriDegisti)?.Yil ?? ilkYil;
            birimler.Add(new BirimDonemi(b.BirimKod, b.BirimAdi, baslangic, sonYil, durumlar));
        }

        return new SinirAnaliziSonucu(esikler, yillar, birimler);
    }

    private static SinirDurumu Otomatik(decimal? degisim, bool yeniMahalleVar, SinirEsikleri e)
    {
        if (!yeniMahalleVar || degisim is not { } d) return SinirDurumu.Sabit;
        if (d <= -e.Dusus || d >= e.Artis) return SinirDurumu.SiniriDegisti;
        if (Math.Abs(d) > e.Supheli) return SinirDurumu.Supheli;
        return SinirDurumu.Sabit;
    }
}
