using SecimSistemi.Domain;

namespace SecimSistemi.Tests.Analiz;

public class MahalleDurumKarariTestleri
{
    private static readonly DateTimeOffset Zaman = new(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(3));
    private static readonly Ilce Ilce = new() { Id = 1, Kod = "34-ESENYURT", Ad = "ESENYURT" };
    private static readonly MahalleKoy A = new() { Id = 10, Kod = 119325, Ad = "ATATÜRK MAH.", IlceId = 1, Ilce = Ilce };
    private static readonly MahalleKoy B = new() { Id = 11, Kod = 147438, Ad = "OSMANGAZİ MAH.", IlceId = 1, Ilce = Ilce };
    private static readonly Secim S2019 = new() { Id = 3, Tarih = new DateOnly(2019, 3, 31), Tur = SecimTuru.Yerel, Ad = "2019" };

    private static MahalleDurumKarari Talep(DurumKarariTuru karar = DurumKarariTuru.SiniriDegisti) =>
        MahalleDurumKarari.DurumTalebi(A, S2019, karar, "Belediye meclisi kararıyla bölündü", "Meclis kararı 2017/45", "ayşe", Zaman);

    [Fact]
    public void Talep_OnaylanincaGecerli()
    {
        var k = Talep();
        Assert.Equal(KararDurumu.Talep, k.Durum);
        Assert.False(k.GecerliMi);

        k.Onayla("mehmet", Zaman, "uygun", null);

        Assert.True(k.GecerliMi);
        Assert.Equal(("ayşe", "mehmet", "Meclis kararı 2017/45"), (k.TalepEden, k.Onaylayan, k.Kaynak));
        Assert.Equal(TimeSpan.Zero, k.KararZamani!.Value.Offset); // UTC saklanır
    }

    [Fact]
    public void YeniKarar_EskisininYerineGecer_EskisiSaklanir()
    {
        var eski = Talep(DurumKarariTuru.SiniriDegisti);
        eski.Onayla("mehmet", Zaman, null, null);
        var yeni = Talep(DurumKarariTuru.Sabit);

        yeni.Onayla("mehmet", Zaman, "yeniden incelendi", eski);

        Assert.True(yeni.GecerliMi);
        Assert.False(eski.GecerliMi);
        Assert.Same(yeni, eski.YerineGecenKarar);
        Assert.Equal(KararDurumu.Onaylandi, eski.Durum); // durum değişmez, yalnızca yerine geçilir
    }

    [Fact]
    public void YalnizcaTalepOnaylanirYaDaReddedilir()
    {
        var k = Talep();
        k.Reddet("mehmet", Zaman, "belge yok");

        Assert.Equal(KararDurumu.Reddedildi, k.Durum);
        Assert.Throws<InvalidOperationException>(() => k.Onayla("mehmet", Zaman, null, null));
    }

    [Fact]
    public void BaskaKonudakiKararinYerineGecilemez()
    {
        var eslemeKarari = MahalleDurumKarari.EslemeTalebi(A, B, EslemeTuru.Bolunme, "g", null, "ayşe", Zaman);
        eslemeKarari.Onayla("mehmet", Zaman, null, null);

        Assert.Throws<InvalidOperationException>(() => Talep().Onayla("mehmet", Zaman, null, eslemeKarari));
    }

    [Fact]
    public void Esleme_YondenBagimsizAyniKonu()
    {
        var ab = MahalleDurumKarari.EslemeTalebi(A, B, EslemeTuru.Bolunme, "g", null, "ayşe", Zaman);
        var ba = MahalleDurumKarari.EslemeTalebi(B, A, EslemeTuru.Bolunme, "g", null, "ayşe", Zaman);

        Assert.True(ab.AyniKonu(ba));
        Assert.False(ab.AyniKonu(Talep()));
    }

    [Fact]
    public void ZorunluAlanlar()
    {
        Assert.Throws<ArgumentException>(() => MahalleDurumKarari.DurumTalebi(A, S2019, DurumKarariTuru.Sabit, " ", null, "ayşe", Zaman));
        Assert.Throws<ArgumentException>(() => MahalleDurumKarari.DurumTalebi(A, S2019, DurumKarariTuru.Esleme, "g", null, "ayşe", Zaman));
        Assert.Throws<ArgumentException>(() => MahalleDurumKarari.EslemeTalebi(A, A, EslemeTuru.Bolunme, "g", null, "ayşe", Zaman));
        var baskaIlce = new MahalleKoy { Id = 99, Kod = 1, Ad = "BAŞKA", IlceId = 2 };
        Assert.Throws<ArgumentException>(() => MahalleDurumKarari.EslemeTalebi(A, baskaIlce, EslemeTuru.Bolunme, "g", null, "ayşe", Zaman));
    }
}
