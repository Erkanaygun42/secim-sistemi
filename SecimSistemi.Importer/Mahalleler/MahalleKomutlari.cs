using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Importer.Mahalleler;

/// <summary><c>mahalle listele | esleme-oner | esleme-yukle &lt;ilçe kodu&gt;</c> komutları.</summary>
internal static class MahalleKomutlari
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public static async Task<int> ListeleAsync(string ilceKodu, SinirEsikleri esikler)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var (ilce, veri) = await VeriOkuAsync(db, ilceKodu);
        var sinir = await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, esikler, birimDuzeyinde: false);

        Console.WriteLine($"{ilce.Ad} ({ilce.Kod}): {veri.Mahalleler.Count} mahalle/köy. Hücreler: sandık sayısı / meclis oyu / sınır durumu; — = o seçimde yok.");
        Console.WriteLine($"Durum: S sabit · Y yeni · Ş şüpheli · D sınırı değişti · ✓ insan kararıyla doğrulandı. Eşikler: {EsikMetni(esikler)}");
        Console.WriteLine();
        var basliklar = new List<string> { "Kod", "Ad" };
        basliklar.AddRange(veri.Yillar.Select(y => y.ToString(CultureInfo.InvariantCulture)));
        basliklar.Add("Dönem");
        var satirlar = veri.Mahalleler
            .OrderBy(m => m.Yillar.Keys.Min()).ThenBy(m => m.Ad, StringComparer.Create(Turkce, false))
            .Select(m =>
            {
                var donem = sinir.Birim(m.Kod);
                var s = new List<string> { m.Kod.ToString(CultureInfo.InvariantCulture), m.Ad };
                s.AddRange(veri.Yillar.Select(y =>
                {
                    if (!m.Yillar.TryGetValue(y, out var v)) return "—";
                    var d = donem?.Yillar.FirstOrDefault(x => x.Yil == y);
                    return string.Create(Turkce, $"{v.SandikSayisi} / {v.MeclisOyu:N0} {(d is null ? "" : DurumKisaltmasi(d))}");
                }));
                s.Add(donem?.Donem ?? "son seçimde yok");
                return (IReadOnlyList<string>)s;
            })
            .ToList();
        Tablo.Yaz(basliklar, satirlar, sagaYasli: Enumerable.Range(2, veri.Yillar.Count).ToHashSet());

        Console.WriteLine();
        Console.WriteLine($"Yalnızca bazı seçimlerde görünen: {veri.EksikYillilar.Count()}");
        var isaretliler = sinir.Birimler
            .SelectMany(b => b.Yillar.Select(y => (Birim: b, Durum: y)))
            .Where(x => x.Durum.Sonuc is SinirDurumu.SiniriDegisti or SinirDurumu.Supheli)
            .GroupBy(x => (x.Durum.Yil, x.Durum.Sonuc))
            .OrderBy(g => g.Key.Yil).ThenByDescending(g => g.Key.Sonuc);
        foreach (var g in isaretliler)
        {
            Console.WriteLine($"{g.Key.Yil} {DurumAdi(g.Key.Sonuc)} ({g.Count()}): " + string.Join(", ",
                g.OrderBy(x => x.Durum.PayDegisimi).Select(x => string.Create(Turkce, $"{x.Birim.BirimAdi} {x.Durum.PayDegisimi * 100:+0;-0}%"))));
        }
        return 0;
    }

    internal static string EsikMetni(SinirEsikleri e) => string.Create(Turkce,
        $"düşüş %{e.Dusus * 100:0}, artış %{e.Artis * 100:0}, şüpheli %{e.Supheli * 100:0} (pay değişimi; yalnızca ilçede yeni mahalle çıkan seçimlerde)");

    internal static string DurumKisaltmasi(BirimYilDurumu d) => (d.Sonuc switch
    {
        SinirDurumu.Sabit => "S",
        SinirDurumu.Yeni => "Y",
        SinirDurumu.Supheli => "Ş",
        SinirDurumu.SiniriDegisti => "D",
        _ => "",
    }) + (d.Dogrulandi ? "✓" : "");

    internal static string DurumAdi(SinirDurumu d) => d switch
    {
        SinirDurumu.Sabit => "sabit",
        SinirDurumu.Yeni => "yeni",
        SinirDurumu.Supheli => "şüpheli",
        SinirDurumu.SiniriDegisti => "sınırı değişti",
        _ => "sonradan kuruldu",
    };

    public static async Task<int> EslemeOnerAsync(string ilceKodu)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var (ilce, veri) = await VeriOkuAsync(db, ilceKodu);

        var yol = MahalleEslemeDosyasi.VarsayilanYol;
        var mevcut = MahalleEslemeDosyasi.Yukle(yol);
        var oneriler = EslemeOnerici.Oner(veri);
        var (satirlar, eklenen) = MahalleEslemeDosyasi.OnerileriEkle(mevcut, oneriler);
        MahalleEslemeDosyasi.Kaydet(yol, satirlar);

        Console.WriteLine($"{ilce.Ad}: yalnızca bazı seçimlerde görünen {veri.EksikYillilar.Count()} mahalle için " +
                          $"benzerliği en az {EslemeOnerici.EnAzBenzerlik:0.00} olan {oneriler.Count} öneri bulundu; {eklenen} tanesi yeni olarak eklendi.");
        Console.WriteLine($"Dosya: {yol}");
        Console.WriteLine("Öneriler onaylı sayılmaz. Doğruladığınız satırda durum'u 'onayli' yapıp tur'u doldurun, sonra: mahalle esleme-yukle " + ilce.Kod);
        foreach (var o in oneriler)
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {o.EskiKod} {o.EskiAd} → {o.YeniKod} {o.YeniAd} (benzerlik {o.Benzerlik:0.00})"));
        return 0;
    }

    /// <summary>
    /// csv'deki onaylı satırları karar tablosuna onaylanmış eşleme kararı olarak aktarır ve birimleri yeniden kurar.
    /// Karar tablosu eşlemelerin tek kaynağıdır: aynı çift (yönden bağımsız) için geçerli bir eşleme kararı varsa satır atlanır;
    /// csv'den silinen satır eşlemeyi kaldırmaz (kararlar silinmez). Hepsi tek transaction içinde.
    /// </summary>
    public static async Task<int> EslemeYukleAsync(string ilceKodu, string kisi)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var ilce = await IlceBulAsync(db, ilceKodu);
        var onayli = MahalleEslemeDosyasi.Yukle().Where(s => s.Durum == EslemeDurumu.Onayli).ToList();

        var kodlar = onayli.SelectMany(s => new[] { s.EskiKod, s.YeniKod }).Distinct().ToList();
        var mahalleler = await db.MahalleKoyler.Where(m => kodlar.Contains(m.Kod)).ToDictionaryAsync(m => m.Kod);
        var bilinmeyen = kodlar.Where(k => !mahalleler.ContainsKey(k)).ToList();
        if (bilinmeyen.Count > 0)
            throw new YuklemeHatasi($"Onaylı eşlemelerdeki şu Muhtarlık Id'ler veritabanında yok: {string.Join(", ", bilinmeyen)}.");

        var buIlce = onayli.Where(s => mahalleler[s.EskiKod].IlceId == ilce.Id || mahalleler[s.YeniKod].IlceId == ilce.Id).ToList();
        var karisik = buIlce.Where(s => mahalleler[s.EskiKod].IlceId != mahalleler[s.YeniKod].IlceId).ToList();
        if (karisik.Count > 0)
            throw new YuklemeHatasi("Şu eşlemeler iki farklı ilçeyi bağlıyor: " +
                                    string.Join(", ", karisik.Select(s => $"{s.EskiKod} → {s.YeniKod}")) + ".");

        var gecerliEslemeler = await db.MahalleDurumKararlari
            .Where(k => k.Karar == DurumKarariTuru.Esleme && k.Durum == KararDurumu.Onaylandi && k.YerineGecenKararId == null
                        && k.MahalleKoy.IlceId == ilce.Id)
            .Select(k => new { k.MahalleKoyId, IliskiliMahalleKoyId = k.IliskiliMahalleKoyId!.Value })
            .ToListAsync();
        var ciftler = gecerliEslemeler
            .SelectMany(e => new[] { (e.MahalleKoyId, e.IliskiliMahalleKoyId), (e.IliskiliMahalleKoyId, e.MahalleKoyId) })
            .ToHashSet();

        await using var tx = await db.Database.BeginTransactionAsync();
        var zaman = DateTimeOffset.UtcNow;
        int aktarilan = 0;
        foreach (var s in buIlce)
        {
            var (eski, yeni) = (mahalleler[s.EskiKod], mahalleler[s.YeniKod]);
            if (!ciftler.Add((eski.Id, yeni.Id)) || !ciftler.Add((yeni.Id, eski.Id))) continue;
            var karar = MahalleDurumKarari.EslemeTalebi(eski, yeni, s.Tur!.Value,
                gerekce: s.Not.Length > 0 ? s.Not : "mahalle-eslemeleri.csv dosyasında onaylı satır",
                kaynak: "Tanimlar/mahalle-eslemeleri.csv", talepEden: kisi, zaman, s.Tarih);
            karar.Onayla(kisi, zaman, "csv'den aktarıldı", oncekiGecerli: null);
            db.MahalleDurumKararlari.Add(karar);
            aktarilan++;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var birimler = await BirimKurucu.YenidenKurAsync(db, ilce.Id);
        await tx.CommitAsync();

        var mahalleSayisi = birimler.Sum(b => b.MahalleKodlari.Count);
        Console.WriteLine($"{ilce.Ad}: csv'de {buIlce.Count} onaylı satır, {aktarilan} tanesi karar tablosuna yeni aktarıldı; " +
                          $"{mahalleSayisi} mahalle {birimler.Count} karşılaştırma birimine ayrıldı.");
        foreach (var b in birimler.Where(b => b.MahalleKodlari.Count > 1))
            Console.WriteLine($"  birim {b.Kod}: {b.Ad}");
        return 0;
    }

    internal static async Task<Ilce> IlceBulAsync(SecimDbContext db, string ilceKodu) =>
        await db.Ilceler.AsNoTracking().FirstOrDefaultAsync(i => i.Kod == ilceKodu)
        ?? throw new YuklemeHatasi($"'{ilceKodu}' kodlu ilçe veritabanında yok. Kayıtlı ilçeler: " +
                                   string.Join(", ", await db.Ilceler.Select(i => i.Kod).ToListAsync()) + ".");

    /// <summary>İlçedeki her mahallenin yerel seçimlerdeki sandık sayısı ve meclis oyu.</summary>
    private static async Task<(Ilce, IlceMahalleVerisi)> VeriOkuAsync(SecimDbContext db, string ilceKodu)
    {
        var ilce = await IlceBulAsync(db, ilceKodu);

        var sandiklar = await db.Sandiklar.AsNoTracking()
            .Where(s => s.MahalleKoy.IlceId == ilce.Id && s.Secim.Tur == SecimTuru.Yerel)
            .GroupBy(s => new { s.MahalleKoyId, s.Secim.Tarih })
            .Select(g => new { g.Key.MahalleKoyId, g.Key.Tarih, Sayi = g.Count() })
            .ToListAsync();
        var oylar = await db.Sonuclar.AsNoTracking()
            .Where(r => r.IlceId == ilce.Id && r.OyTuru == OyTuru.BelediyeMeclisi && r.MahalleKoyId != null && r.Secim.Tur == SecimTuru.Yerel)
            .GroupBy(r => new { r.MahalleKoyId, r.Secim.Tarih })
            .Select(g => new { g.Key.MahalleKoyId, g.Key.Tarih, Oy = g.Sum(x => (long)x.Oy) })
            .ToListAsync();
        var mahalleler = await db.MahalleKoyler.AsNoTracking().Where(m => m.IlceId == ilce.Id).ToListAsync();

        var liste = mahalleler.Select(m => new MahalleYillari(m.Kod, m.Ad,
                sandiklar.Where(s => s.MahalleKoyId == m.Id).ToDictionary(
                    s => s.Tarih.Year,
                    s => new MahalleYilVerisi(s.Sayi, oylar.FirstOrDefault(o => o.MahalleKoyId == m.Id && o.Tarih == s.Tarih)?.Oy ?? 0))))
            .Where(m => m.Yillar.Count > 0)
            .ToList();
        var yillar = sandiklar.Select(s => s.Tarih.Year).Distinct().Order().ToList();
        if (yillar.Count == 0)
            throw new YuklemeHatasi($"{ilce.Ad} için yüklenmiş yerel seçim yok.");
        return (ilce, new IlceMahalleVerisi(yillar, liste));
    }
}
