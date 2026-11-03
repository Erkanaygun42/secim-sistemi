using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Domain;

namespace SecimSistemi.Importer.Mahalleler;

/// <summary>
/// Mahalle durum kararları: <c>karar-talep</c>, <c>karar-onayla</c>, <c>karar-listele</c>.
/// Rol ve yetki kontrolü yok (Sprint 7, kullanıcı girişi); kişi adı --kisi ile serbest metin olarak yazılır.
/// </summary>
internal static class KararKomutlari
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly Dictionary<string, DurumKarariTuru> DurumKararlari = new()
    {
        ["sabit"] = DurumKarariTuru.Sabit,
        ["siniri-degisti"] = DurumKarariTuru.SiniriDegisti,
    };

    private static readonly Dictionary<string, EslemeTuru> EslemeTurleri = new()
    {
        ["bolunme"] = EslemeTuru.Bolunme,
        ["birlesme"] = EslemeTuru.Birlesme,
        ["ad-degisikligi"] = EslemeTuru.AdDegisikligi,
        ["kod-degisikligi"] = EslemeTuru.KodDegisikligi,
    };

    public static async Task<int> TalepAsync(string ilceKodu, Secenekler s)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var ilce = await MahalleKomutlari.IlceBulAsync(db, ilceKodu);
        var mahalle = await MahalleAsync(db, ilce, s.Tamsayi("--mahalle", "kararın konusu olan mahallenin Muhtarlık Id'si"));
        var gerekce = s.Zorunlu("--gerekce", "kararın gerekçesi");
        var kisi = s.Zorunlu("--kisi", "talep eden kişi");
        var kaynak = s.Deger("--kaynak");
        var zaman = DateTimeOffset.UtcNow;

        MahalleDurumKarari talep;
        if (s.Deger("--esle") is not null)
        {
            if (s.Deger("--karar") is not null || s.Deger("--secim") is not null)
                throw new YuklemeHatasi("Eşleme talebinde --karar ve --secim verilmez (eşleme seçime değil mahalle çiftine bağlıdır).");
            var yeni = await MahalleAsync(db, ilce, s.Tamsayi("--esle", "eşlenecek (yeni) mahallenin Muhtarlık Id'si"));
            var turAdi = s.Zorunlu("--tur", string.Join(" | ", EslemeTurleri.Keys));
            if (!EslemeTurleri.TryGetValue(turAdi, out var tur))
                throw new YuklemeHatasi($"--tur '{turAdi}' olamaz: {string.Join(", ", EslemeTurleri.Keys)}.");
            DateOnly? yururluk = s.Deger("--yururluk") is { } y
                ? DateOnly.TryParseExact(y, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t
                  : throw new YuklemeHatasi($"--yururluk '{y}' yyyy-aa-gg biçiminde olmalı.")
                : null;
            talep = MahalleDurumKarari.EslemeTalebi(mahalle, yeni, tur, gerekce, kaynak, kisi, zaman, yururluk);
        }
        else
        {
            var kararAdi = s.Zorunlu("--karar", "sabit | siniri-degisti (eşleme için --esle)");
            if (!DurumKararlari.TryGetValue(kararAdi, out var karar))
                throw new YuklemeHatasi($"--karar '{kararAdi}' olamaz: sabit, siniri-degisti (eşleme için --esle <kod> --tur ...).");
            var yil = s.Tamsayi("--secim", "kararın geçerli olduğu yerel seçimin yılı");
            var secim = await db.Secimler.SingleOrDefaultAsync(x => x.Tur == SecimTuru.Yerel && x.Tarih.Year == yil)
                ?? throw new YuklemeHatasi($"{yil} yılında yüklenmiş yerel seçim yok.");
            if (!await db.Sandiklar.AnyAsync(x => x.SecimId == secim.Id && x.MahalleKoyId == mahalle.Id))
                throw new YuklemeHatasi($"{mahalle.Ad} {yil} seçiminde yok; o seçim için sınır kararı verilemez.");
            talep = MahalleDurumKarari.DurumTalebi(mahalle, secim, karar, gerekce, kaynak, kisi, zaman);
        }

        // Aynı konuda bekleyen bir talep varsa ikincisi açılmaz; karışıklık olmasın.
        var bekleyen = (await AyniMahalleKararlariAsync(db, talep)).FirstOrDefault(k => k.Durum == KararDurumu.Talep && k.AyniKonu(talep));
        if (bekleyen is not null)
            throw new YuklemeHatasi($"Aynı konuda bekleyen talep var: #{bekleyen.Id}. Önce onu onaylayın ya da reddedin.");

        db.MahalleDurumKararlari.Add(talep);
        await db.SaveChangesAsync();
        Console.WriteLine($"Talep #{talep.Id} oluşturuldu: {Ozet(talep)}. Onay için: karar-onayla {talep.Id} --kisi <ad>");
        return 0;
    }

    public static async Task<int> OnaylaAsync(string kararNo, Secenekler s)
    {
        if (!int.TryParse(kararNo, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new YuklemeHatasi($"Karar numarası '{kararNo}' bir tam sayı olmalı.");
        var kisi = s.Zorunlu("--kisi", "onaylayan ya da reddeden kişi");

        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var karar = await db.MahalleDurumKararlari
            .Include(k => k.MahalleKoy).Include(k => k.IliskiliMahalleKoy).Include(k => k.Secim)
            .SingleOrDefaultAsync(k => k.Id == id)
            ?? throw new YuklemeHatasi($"#{id} numaralı karar yok.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var zaman = DateTimeOffset.UtcNow;
        try
        {
            if (s.Bayrak("--reddet"))
            {
                karar.Reddet(kisi, zaman, s.Deger("--not"));
                await db.SaveChangesAsync();
            }
            else
            {
                var onceki = (await AyniMahalleKararlariAsync(db, karar)).FirstOrDefault(k => k.Id != karar.Id && k.GecerliMi && k.AyniKonu(karar));
                karar.Onayla(kisi, zaman, s.Deger("--not"), onceki);

                // "Bir konuda tek geçerli karar" indeksi yüzünden önce eski karar (yerine geçildi), sonra yenisi kaydedilir.
                if (onceki is not null)
                    await db.MahalleDurumKararlari.Where(k => k.Id == onceki.Id)
                        .ExecuteUpdateAsync(u => u.SetProperty(k => k.YerineGecenKararId, karar.Id));
                await db.SaveChangesAsync();

                if (karar.Karar == DurumKarariTuru.Esleme)
                {
                    db.ChangeTracker.Clear();
                    await BirimKurucu.YenidenKurAsync(db, karar.MahalleKoy.IlceId);
                }
                if (onceki is not null)
                    Console.WriteLine($"#{onceki.Id} numaralı karar artık geçerli değil (#{karar.Id} yerine geçti); geçmişte saklanıyor.");
            }
        }
        catch (InvalidOperationException hata)
        {
            throw new YuklemeHatasi(hata.Message);
        }
        await tx.CommitAsync();

        Console.WriteLine($"#{karar.Id} {(karar.Durum == KararDurumu.Onaylandi ? "onaylandı" : "reddedildi")}: {Ozet(karar)}.");
        if (karar.Durum == KararDurumu.Onaylandi && karar.Karar == DurumKarariTuru.Esleme)
            Console.WriteLine("Karşılaştırma birimleri yeniden kuruldu.");
        return 0;
    }

    public static async Task<int> ListeleAsync(string ilceKodu, bool hepsi)
    {
        await using var db = new SecimDbContext(await Veritabani.HazirlaAsync());
        var ilce = await MahalleKomutlari.IlceBulAsync(db, ilceKodu);
        var kararlar = await db.MahalleDurumKararlari.AsNoTracking()
            .Include(k => k.MahalleKoy).Include(k => k.IliskiliMahalleKoy).Include(k => k.Secim)
            .Where(k => k.MahalleKoy.IlceId == ilce.Id)
            .OrderBy(k => k.Id)
            .ToListAsync();
        var gosterilen = hepsi ? kararlar : kararlar.Where(k => k.Durum == KararDurumu.Talep || k.GecerliMi).ToList();

        Console.WriteLine($"{ilce.Ad}: {kararlar.Count} karar ({kararlar.Count(k => k.Durum == KararDurumu.Talep)} bekleyen talep, " +
                          $"{kararlar.Count(k => k.GecerliMi)} geçerli). {(hepsi ? "Geçmiş dahil." : "Geçmiş için --hepsi.")}");
        if (gosterilen.Count == 0) return 0;
        Console.WriteLine();

        string[] sutunlar = ["#", "Durum", "Karar", "Mahalle", "Seçim / eşlenen", "Gerekçe", "Kaynak", "Talep eden", "Karar veren", "Tarih"];
        var satirlar = gosterilen.Select(k => (IReadOnlyList<string>)
        [
            k.Id.ToString(CultureInfo.InvariantCulture),
            k.Durum switch
            {
                KararDurumu.Talep => "talep",
                KararDurumu.Reddedildi => "reddedildi",
                _ when k.YerineGecenKararId is { } y => $"yerine #{y} geçti",
                _ => "geçerli",
            },
            k.Karar switch
            {
                DurumKarariTuru.Sabit => "sabit",
                DurumKarariTuru.SiniriDegisti => "sınırı değişti",
                _ => $"eşleme ({k.EslemeTuru})",
            },
            $"{k.MahalleKoy.Kod} {k.MahalleKoy.Ad}",
            k.Karar == DurumKarariTuru.Esleme ? $"→ {k.IliskiliMahalleKoy!.Kod} {k.IliskiliMahalleKoy.Ad}" : k.Secim!.Tarih.Year.ToString(CultureInfo.InvariantCulture),
            Kisalt(k.Gerekce, 50),
            Kisalt(k.Kaynak ?? "", 30),
            k.TalepEden,
            k.Onaylayan ?? "",
            (k.KararZamani ?? k.TalepZamani).ToLocalTime().ToString("dd.MM.yyyy HH:mm", Turkce),
        ]).ToList();
        Tablo.Yaz(sutunlar, satirlar, sagaYasli: new HashSet<int> { 0 });
        return 0;
    }

    private static async Task<MahalleKoy> MahalleAsync(SecimDbContext db, Ilce ilce, int kod) =>
        await db.MahalleKoyler.SingleOrDefaultAsync(m => m.Kod == kod && m.IlceId == ilce.Id)
        ?? throw new YuklemeHatasi($"{ilce.Ad} ilçesinde {kod} Muhtarlık Id'li mahalle/köy yok.");

    /// <summary>Kararın mahallelerine (konu ve ilişkili) dokunan bütün kararlar; aynı konu süzgeci çağırana kalır.</summary>
    private static async Task<List<MahalleDurumKarari>> AyniMahalleKararlariAsync(SecimDbContext db, MahalleDurumKarari karar)
    {
        var idler = new[] { karar.MahalleKoyId, karar.IliskiliMahalleKoyId ?? karar.MahalleKoyId };
        return await db.MahalleDurumKararlari
            .Where(k => idler.Contains(k.MahalleKoyId) || (k.IliskiliMahalleKoyId != null && idler.Contains(k.IliskiliMahalleKoyId.Value)))
            .ToListAsync();
    }

    private static string Ozet(MahalleDurumKarari k) => k.Karar switch
    {
        DurumKarariTuru.Esleme => $"{k.MahalleKoy.Ad} → {k.IliskiliMahalleKoy!.Ad} eşlemesi ({k.EslemeTuru})",
        DurumKarariTuru.Sabit => $"{k.MahalleKoy.Ad} {k.Secim!.Tarih.Year} seçiminde sabit",
        _ => $"{k.MahalleKoy.Ad} {k.Secim!.Tarih.Year} seçiminde sınırı değişti",
    };

    private static string Kisalt(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}
