using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Mahalleler;

/// <summary><c>mahalle listele | esleme-oner | esleme-yukle &lt;ilçe kodu&gt;</c> komutları.</summary>
internal static class MahalleKomutlari
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public static async Task<int> ListeleAsync(string ilceKodu)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var (ilce, veri) = await VeriOkuAsync(db, ilceKodu);

        Console.WriteLine($"{ilce.Ad} ({ilce.Kod}): {veri.Mahalleler.Count} mahalle/köy. Hücreler: sandık sayısı / meclis oyu; — = o seçimde yok.");
        Console.WriteLine();
        var basliklar = new List<string> { "Kod", "Ad" };
        basliklar.AddRange(veri.Yillar.Select(y => y.ToString(CultureInfo.InvariantCulture)));
        basliklar.Add("Durum");
        var satirlar = veri.Mahalleler
            .OrderBy(m => m.Yillar.Keys.Min()).ThenBy(m => m.Ad, StringComparer.Create(Turkce, false))
            .Select(m =>
            {
                var s = new List<string> { m.Kod.ToString(CultureInfo.InvariantCulture), m.Ad };
                s.AddRange(veri.Yillar.Select(y => m.Yillar.TryGetValue(y, out var v)
                    ? string.Create(Turkce, $"{v.SandikSayisi} / {v.MeclisOyu:N0}")
                    : "—"));
                var eksik = veri.Yillar.Where(y => !m.Yillar.ContainsKey(y)).ToList();
                s.Add(eksik.Count == 0 ? "her seçimde" : "yalnızca " + string.Join(", ", m.Yillar.Keys.Order()));
                return (IReadOnlyList<string>)s;
            })
            .ToList();
        Tablo.Yaz(basliklar, satirlar, sagaYasli: Enumerable.Range(2, veri.Yillar.Count).ToHashSet());

        Console.WriteLine();
        Console.WriteLine($"Yalnızca bazı seçimlerde görünen: {veri.EksikYillilar.Count()}");
        Console.WriteLine();
        Console.WriteLine("İpuçları (eşleme girerken bakın; hiçbiri kendiliğinden kaydedilmez):");
        foreach (var satir in EslemeOnerici.Ipuclari(veri))
            Console.WriteLine("  " + satir);
        return 0;
    }

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
    /// csv'deki onaylı satırları ilçenin eşlemeleri olarak yazar (csv esas alınır: csv'de olmayan eşleme silinir)
    /// ve karşılaştırma birimlerini yeniden kurar. Hepsi tek transaction içinde.
    /// </summary>
    public static async Task<int> EslemeYukleAsync(string ilceKodu)
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

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.MahalleKoyEslemeleri
            .Where(e => e.EskiMahalleKoy.IlceId == ilce.Id || e.YeniMahalleKoy.IlceId == ilce.Id)
            .ExecuteDeleteAsync();
        foreach (var s in buIlce)
        {
            db.MahalleKoyEslemeleri.Add(new MahalleKoyEslemesi
            {
                EskiMahalleKoyId = mahalleler[s.EskiKod].Id,
                YeniMahalleKoyId = mahalleler[s.YeniKod].Id,
                Tur = s.Tur!.Value,
                GecerlilikTarihi = s.Tarih,
                Not = s.Not.Length == 0 ? null : s.Not,
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var birimler = await BirimKurucu.YenidenKurAsync(db, ilce.Id);
        await tx.CommitAsync();

        var mahalleSayisi = birimler.Sum(b => b.MahalleKodlari.Count);
        Console.WriteLine($"{ilce.Ad}: {buIlce.Count} onaylı eşleme yazıldı; {mahalleSayisi} mahalle {birimler.Count} karşılaştırma birimine ayrıldı.");
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
