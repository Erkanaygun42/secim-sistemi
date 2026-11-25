using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

/// <summary>Karnede seçilen yıla göre dönem ve şüpheli uyarısı; karne sıralaması.</summary>
public class KarneKurallariTestleri
{
    private static BirimYilDurumu D(int yil, SinirDurumu sonuc, string? karar = null) => new(yil, sonuc, sonuc, null, null, karar);

    // 2009 sabit, 2014 şüpheli, 2019 sınırı değişti, 2024 şüpheli.
    private static readonly BirimDonemi Birim = new(1, "A", 2019, 2024,
        [D(2009, SinirDurumu.Sabit), D(2014, SinirDurumu.Supheli), D(2019, SinirDurumu.SiniriDegisti), D(2024, SinirDurumu.Supheli)]);

    [Theory]
    [InlineData(2009, 2009)]
    [InlineData(2014, 2009)]
    [InlineData(2019, 2019)]
    [InlineData(2024, 2019)]
    public void DonemBaslangici_SecilenYilaKadarkiSonKesinti(int yil, int beklenen) =>
        Assert.Equal(beklenen, Birim.DonemBaslangiciYil(yil));

    [Fact]
    public void SonSecimde_DonemBaslangiciIleAyni() =>
        Assert.Equal(Birim.DonemBaslangici, Birim.DonemBaslangiciYil(2024));

    [Fact]
    public void SupheliYillar_YalnizcaDonemIcindekiler()
    {
        Assert.Equal([2014], Birim.SupheliYillar(2014).Select(d => d.Yil));
        Assert.Empty(Birim.SupheliYillar(2019)); // 2014 şüphesi 2019 kesintisinden önce kaldı
        Assert.Equal([2024], Birim.SupheliYillar(2024).Select(d => d.Yil));
    }

    [Fact]
    public void InsanKarariOlanYil_SupheliSayilmaz()
    {
        var b = new BirimDonemi(1, "A", 2009, 2019,
            [D(2009, SinirDurumu.Sabit), D(2014, SinirDurumu.Sabit, "karar #3"), D(2019, SinirDurumu.Sabit)]);

        Assert.Empty(b.SupheliYillar(2019));
    }

    // ---------------- Dönemin sınır özeti ----------------

    [Theory]
    [InlineData(2009, SinirDurumu.Sabit, new int[0])]
    [InlineData(2014, SinirDurumu.Supheli, new[] { 2014 })]
    [InlineData(2019, SinirDurumu.SiniriDegisti, new[] { 2019 })]
    [InlineData(2024, SinirDurumu.Supheli, new[] { 2024 })]
    public void DonemSiniri_YilinDegilDonemin(int yil, SinirDurumu beklenen, int[] yillar)
    {
        var d = Birim.DonemSiniri(yil)!;

        Assert.Equal(beklenen, d.Durum);
        Assert.Equal(yillar, d.Yillar);
    }

    [Fact]
    public void DonemSiniri_OYilSabitAmaDonemdeSupheliVarsa_Supheli()
    {
        // 2019 şüpheli, 2024 sabit: 2024 karnesinde "Sabit" değil "Şüpheli (2019)".
        var b = new BirimDonemi(1, "A", 2009, 2024,
            [D(2009, SinirDurumu.Sabit), D(2014, SinirDurumu.Sabit), D(2019, SinirDurumu.Supheli), D(2024, SinirDurumu.Sabit)]);

        var d = b.DonemSiniri(2024)!;

        Assert.Equal((SinirDurumu.Supheli, 2009, true), (d.Durum, d.DonemBaslangici, d.UyariVar));
        Assert.Equal("Şüpheli (2019)", SecimSistemi.Web.Raporlar.Metinler.Sinir(d));
    }

    [Fact]
    public void DonemSiniri_DonemBirKesintiyleBasladiysa_KesintiYiliyla()
    {
        var b = new BirimDonemi(1, "A", 2019, 2024,
            [D(2009, SinirDurumu.SonradanKuruldu), D(2014, SinirDurumu.SonradanKuruldu), D(2019, SinirDurumu.Yeni), D(2024, SinirDurumu.Sabit)]);

        var d = b.DonemSiniri(2024)!;

        Assert.Equal((SinirDurumu.Yeni, "2019–2024", false), (d.Durum, d.Donem, d.BuSecimdeKesildi));
        Assert.Equal("Yeni (2019)", SecimSistemi.Web.Raporlar.Metinler.Sinir(d));
        Assert.True(b.DonemSiniri(2019)!.BuSecimdeKesildi);
        Assert.Equal(SinirDurumu.SonradanKuruldu, b.DonemSiniri(2014)!.Durum);
    }

    [Fact]
    public void DonemSiniri_InsanKarariylaDogrulananSabit_Isaretli()
    {
        var b = new BirimDonemi(1, "A", 2009, 2019,
            [D(2009, SinirDurumu.Sabit), D(2014, SinirDurumu.Sabit, "karar #3"), D(2019, SinirDurumu.Sabit)]);

        var d = b.DonemSiniri(2019)!;

        Assert.Equal((SinirDurumu.Sabit, true), (d.Durum, d.Dogrulandi));
        Assert.Equal("Sabit ✓", SecimSistemi.Web.Raporlar.Metinler.Sinir(d));
        Assert.Null(b.DonemSiniri(2004)); // analizde olmayan yıl
    }

    // ---------------- Sıralama ----------------

    private static DonemSiniri? Ds(SinirDurumu? d) => d switch
    {
        null => null,
        SinirDurumu.Sabit => new DonemSiniri(SinirDurumu.Sabit, 2009, 2024, []),
        var x => new DonemSiniri(x.Value, 2019, 2024, [D(2019, x.Value)]),
    };

    private static KarneSatiri S(int kod, string ad, decimal? oran, SinirDurumu? sinir = null, decimal? puan = null) =>
        new(kod, ad, 1, 100, oran is not null, oran is null ? null : (long)oran, oran,
            sinir is null ? null : D(2024, SinirDurumu.Sabit), Ds(sinir), puan is null ? null : 2019, puan);

    private static readonly KarneSatiri[] Satirlar =
    [
        S(1, "ÇINAR", 2m, SinirDurumu.Sabit, puan: -0.5m),
        S(2, "CUMHURİYET", null, SinirDurumu.Yeni),
        S(3, "İNÖNÜ", 5m, SinirDurumu.SiniriDegisti, puan: 1m),
        S(4, "AKÇA", 2m, SinirDurumu.Supheli, puan: -2m),
        S(5, "ZAFER", null),
        S(6, "IŞIK", 1m, SinirDurumu.Supheli),
    ];

    [Fact]
    public void BosDegerler_HerIkiYondeEnSonda()
    {
        var azalan = KarneSiralamasi.Uygula(Satirlar, KarneSiraAlani.DspOrani, azalan: true);
        var artan = KarneSiralamasi.Uygula(Satirlar, KarneSiraAlani.DspOrani, azalan: false);

        Assert.Equal([3, 4, 1, 6, 2, 5], azalan.Select(s => s.BirimKod)); // eşit oranda (2) ad sırası: AKÇA, ÇINAR
        Assert.Equal([6, 4, 1, 3, 2, 5], artan.Select(s => s.BirimKod));
    }

    [Fact]
    public void Ad_TurkceAlfabeSirasi()
    {
        var sirali = KarneSiralamasi.Uygula(Satirlar, KarneSiraAlani.Ad, azalan: false);

        // C < Ç, I < İ (Türkçe: ... C, Ç, ... I, İ ...)
        Assert.Equal(["AKÇA", "CUMHURİYET", "ÇINAR", "IŞIK", "İNÖNÜ", "ZAFER"], sirali.Select(s => s.BirimAdi));
    }

    [Fact]
    public void Sinir_DonemeGore_OnceDikkatIsteyenler_DurumuOlmayanEnSonda()
    {
        var sirali = KarneSiralamasi.Uygula(Satirlar, KarneSiraAlani.Sinir, azalan: false);

        // dönemi şüpheli (ad sırası: AKÇA, IŞIK), sınırı değişti, yeni, sabit, durum yok
        Assert.Equal([4, 6, 3, 2, 1, 5], sirali.Select(s => s.BirimKod));
    }

    [Fact]
    public void PuanFarki_EnCokDusenOnceArtanda_BoslarEnSonda()
    {
        var sirali = KarneSiralamasi.Uygula(Satirlar, KarneSiraAlani.PuanFarki, azalan: false);

        Assert.Equal([4, 1, 3], sirali.Take(3).Select(s => s.BirimKod));
        Assert.All(sirali.Skip(3), s => Assert.Null(s.PuanFarki));
    }
}
