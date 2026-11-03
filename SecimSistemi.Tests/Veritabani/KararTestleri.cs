using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Tests.ImporterTestleri;

namespace SecimSistemi.Tests.Veritabani;

/// <summary>Mahalle durum kararlarının veritabanı kuralları ve sınır analizi servisinin kararları kullanması.</summary>
public sealed class KararTestleri : VeritabaniTesti, IDisposable
{
    private const int A = 999001, B = 999002, Yeni = 999003;
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "secim-karar-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Zaman = DateTimeOffset.UtcNow;

    public KararTestleri(TestVeritabani veritabani) : base(veritabani) => Directory.CreateDirectory(Path.Combine(_klasor, "raw"));

    public void Dispose() => Directory.Delete(_klasor, recursive: true);

    /// <summary>
    /// 2014: A 100, B 100. 2019: A 40 (pay %50 → %10, −%80), B 110 (%50 → %27,5, −%45 değil: aşağıya bakın), Yeni 250.
    /// Payları: 2014 A %50, B %50; 2019 A 40/400 = %10 (−%80), B 110/400 = %27,5 (−%45), Yeni %62,5.
    /// </summary>
    private async Task<int> VeriYukleAsync(long b2019 = 110)
    {
        var yukleyici = new Yukleyici(Db, BaslikTanimlari.Yukle(), Path.Combine(_klasor, "arsiv"));
        async Task Yukle(string ad, object?[][] satirlar)
        {
            var yol = Path.Combine(_klasor, "raw", ad);
            using (var akis = YskDosyaOkuyucuTestleri.Dosya(satirlar)) File.WriteAllBytes(yol, akis.ToArray());
            await yukleyici.YukleAsync(yol);
        }
        object?[] baslik = ["Muhtarlık Id", "Muhtarlık Adı", "Sandık No", "AK PARTİ", "DSP"];
        await Yukle("2014_yerel_belediye-meclisi_esenyurt.xlsx", [baslik, [A, "A MAH.", 1001, 99, 1], [B, "B MAH.", 1002, 99, 1]]);
        await Yukle("2019_yerel_belediye-meclisi_esenyurt.xlsx", [baslik, [A, "A MAH.", 1001, 39, 1], [B, "B MAH.", 1002, b2019 - 1, 1], [Yeni, "YENİ MAH.", 1003, 249, 1]]);
        return (await Db.Ilceler.SingleAsync(i => i.Kod == "34-ESENYURT")).Id;
    }

    private async Task<MahalleKoy> M(int kod) => await Db.MahalleKoyler.SingleAsync(m => m.Kod == kod);
    private async Task<Secim> S(int yil) => await Db.Secimler.SingleAsync(s => s.Tarih.Year == yil);

    [Fact]
    public async Task Otomatik_SiniriDegisti_InsanKarariSabitYapar()
    {
        var ilceId = await VeriYukleAsync();
        var servis = new SinirAnaliziServisi(Db);

        var once = await servis.HazirlaAsync(ilceId, SinirEsikleri.Varsayilan);
        Assert.Equal(SinirDurumu.SiniriDegisti, once.Birim(A)!.Yillar.Single(y => y.Yil == 2019).Sonuc);
        Assert.Equal(2019, once.Birim(A)!.DonemBaslangici);

        var karar = MahalleDurumKarari.DurumTalebi(await M(A), await S(2019), DurumKarariTuru.Sabit, "yerinde incelendi", "saha", "ayşe", Zaman);
        karar.Onayla("mehmet", Zaman, null, null);
        Db.MahalleDurumKararlari.Add(karar);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var sonra = await servis.HazirlaAsync(ilceId, SinirEsikleri.Varsayilan);
        var y = sonra.Birim(A)!.Yillar.Single(x => x.Yil == 2019);
        Assert.Equal((SinirDurumu.SiniriDegisti, SinirDurumu.Sabit, true), (y.Otomatik, y.Sonuc, y.Dogrulandi));
        Assert.Contains("yerinde incelendi", y.Karar);
        Assert.Equal(2014, sonra.Birim(A)!.DonemBaslangici);
    }

    [Fact]
    public async Task OnayliEsleme_BirimiBirlestirirVeDonemiUzatir()
    {
        var ilceId = await VeriYukleAsync();
        var esleme = MahalleDurumKarari.EslemeTalebi(await M(A), await M(Yeni), EslemeTuru.Bolunme, "bölündü", null, "ayşe", Zaman);
        esleme.Onayla("mehmet", Zaman, null, null);
        Db.MahalleDurumKararlari.Add(esleme);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        await SecimSistemi.Importer.Mahalleler.BirimKurucu.YenidenKurAsync(Db, ilceId);

        var s = await new SinirAnaliziServisi(Db).HazirlaAsync(ilceId, SinirEsikleri.Varsayilan);

        var birim = s.Birim(A)!;
        Assert.Equal("A MAH. + YENİ MAH.", birim.BirimAdi);
        var y = birim.Yillar.Single(x => x.Yil == 2019);
        Assert.Equal((SinirDurumu.Sabit, true), (y.Sonuc, y.Dogrulandi));
        Assert.Equal(2014, birim.DonemBaslangici);
        Assert.Null(s.Birim(Yeni)); // ayrı birim değil
    }

    [Fact]
    public async Task IkinciGecerliKarar_VeritabaniReddeder()
    {
        await VeriYukleAsync();
        var (a, s2019) = (await M(A), await S(2019));
        foreach (var tur in new[] { DurumKarariTuru.Sabit, DurumKarariTuru.SiniriDegisti })
        {
            var k = MahalleDurumKarari.DurumTalebi(a, s2019, tur, "g", null, "ayşe", Zaman);
            k.Onayla("mehmet", Zaman, null, null);
            Db.MahalleDurumKararlari.Add(k);
        }

        var hata = await KaydetmeReddedilir();
        Assert.Equal(PostgresErrorCodes.UniqueViolation, hata.SqlState);
        Assert.Equal("IX_MahalleDurumKararlari_GecerliDurum", hata.ConstraintName);
    }

    [Fact]
    public async Task YerineGecilenKarar_GecmisteKalir()
    {
        await VeriYukleAsync();
        var (a, s2019) = (await M(A), await S(2019));
        var eski = MahalleDurumKarari.DurumTalebi(a, s2019, DurumKarariTuru.SiniriDegisti, "ilk", null, "ayşe", Zaman);
        eski.Onayla("mehmet", Zaman, null, null);
        Db.MahalleDurumKararlari.Add(eski);
        await Db.SaveChangesAsync();
        var yeni = MahalleDurumKarari.DurumTalebi(a, s2019, DurumKarariTuru.Sabit, "düzeltme", null, "ayşe", Zaman);
        Db.MahalleDurumKararlari.Add(yeni);
        await Db.SaveChangesAsync();

        // karar-onayla komutundaki sıra: önce eski karar (yerine geçildi), sonra yenisi kaydedilir.
        yeni.Onayla("mehmet", Zaman, null, eski);
        await Db.MahalleDurumKararlari.Where(k => k.Id == eski.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(k => k.YerineGecenKararId, yeni.Id));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var kararlar = await Db.MahalleDurumKararlari.OrderBy(k => k.Id).ToListAsync();
        Assert.Equal(2, kararlar.Count);
        Assert.Equal(kararlar[1].Id, kararlar[0].YerineGecenKararId);
        Assert.Equal([false, true], kararlar.Select(k => k.GecerliMi));
    }

    [Fact]
    public async Task Karar_Silinemez()
    {
        await VeriYukleAsync();
        Db.MahalleDurumKararlari.Add(MahalleDurumKarari.DurumTalebi(await M(A), await S(2019), DurumKarariTuru.Sabit, "g", null, "ayşe", Zaman));
        await Db.SaveChangesAsync();

        var hata = await SqlReddedilir($"""DELETE FROM "MahalleDurumKararlari" """);
        Assert.Equal(PostgresErrorCodes.RaiseException, hata.SqlState);
        Assert.Contains("silinemez", hata.MessageText);
    }

    [Fact]
    public async Task TalepteOnaylayanOlamaz_VeritabaniReddeder()
    {
        await VeriYukleAsync();
        var (a, s) = (await M(A), await S(2019));

        var hata = await SqlReddedilir($"""
            INSERT INTO "MahalleDurumKararlari" ("MahalleKoyId", "Karar", "SecimId", "Gerekce", "TalepEden", "TalepZamani", "Durum", "Onaylayan")
            VALUES ({a.Id}, 'Sabit', {s.Id}, 'g', 'ayşe', now(), 'Talep', 'mehmet')
            """);
        Assert.Equal("CK_MahalleDurumKararlari_Onay", hata.ConstraintName);
    }
}
