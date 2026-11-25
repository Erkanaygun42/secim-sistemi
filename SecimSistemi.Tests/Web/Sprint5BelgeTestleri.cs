using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>Gruplar, öncelik ve meclis raporlarının Excel/PDF belgeleri.</summary>
public sealed class Sprint5BelgeTestleri : IAsyncDisposable
{
    private readonly WebApplication _uygulama;
    private readonly PdfUretici _pdf;

    public Sprint5BelgeTestleri()
    {
        var b = WebApplication.CreateBuilder();
        b.Services.AddRazorComponents();
        b.Services.AddSingleton(Gorunum.Varsayilan);
        _uygulama = b.Build();
        _pdf = new PdfUretici(_uygulama.Services.GetRequiredService<IServiceScopeFactory>(), NullLoggerFactory.Instance, Gorunum.Varsayilan);
    }

    public async ValueTask DisposeAsync()
    {
        await _pdf.DisposeAsync();
        await _uygulama.DisposeAsync();
    }

    private static readonly DateTime Uretim = RaporBelgeleriTestleri.Uretim;

    private static GrupSonuclari Gruplar(GrupSorgusu? sorgu = null)
    {
        var e = RaporBelgeleriTestleri.OrnekErime();
        var g = GrupHesabi.Hesapla(e.Rapor!, e.IlceErimesi[OyTuru.BelediyeMeclisi].ToDictionary(x => x.Yil, x => x.Oran), GrupEsikleri.Varsayilan);
        return new GrupSonuclari(e, g, OncelikHesabi.Hesapla(g, e.Sinir), sorgu ?? new GrupSorgusu("34-ESENYURT", "belediye-meclisi"));
    }

    private static MeclisSonuclari Meclis(long ekOy = 0) =>
        MeclisHazirlayici.Hesapla([RaporBelgeleriTestleri.Ilce], RaporBelgeleriTestleri.Ilce, [RaporBelgeleriTestleri.Secim2024],
            RaporBelgeleriTestleri.Secim2024,
            [new("CHP", 221_877), new("AK PARTİ", 176_194), new("ZAFER PARTİSİ", 18_220), new("İYİ PARTİ", 6_385), new("DSP", 975), new("SAADET", 3_003)],
            new MeclisSorgusu(null, null, null, ekOy == 0 ? null : ekOy, null),
            new MeclisUyeTanimlari([new MeclisUyeSayisi("34-ESENYURT", 2024, 45, false, "kullanıcı varsayımı")]));

    [Fact]
    public void Gruplar_UcBolum_2x2GrafikliVeEsikMetni()
    {
        var b = RaporBelgeleri.Gruplar(Gruplar(), KaynakListesi.Bos, Uretim);

        Assert.Equal(["Gruplar", "2x2", "Mahalleler"], b.Bolumler.Select(x => x.SayfaAdi));
        Assert.Contains("en az 10 oy ve oranın en az %30 değişmesi", b.Bolumler[0].Aciklama);
        Assert.IsType<DagilimGrafigi>(Assert.Single(b.Bolumler[1].GrafikListesi));
        Assert.Equal(4, b.Bolumler[1].Satirlar.Count);
        Assert.Equal("gruplar_34-ESENYURT_belediye-meclisi", b.DosyaAdi);
        Assert.Contains(b.Okuma, t => t.Ad == "Bugünkü güç");
        Assert.Contains(b.Okuma, t => t.Ad == "Dönem başındaki güç");
    }

    [Fact]
    public void Oncelik_SuzgecUygulanir_DosyaAdindaSuzgec()
    {
        var hepsi = RaporBelgeleri.Oncelik(Gruplar(), KaynakListesi.Bos, Uretim);
        var kale = RaporBelgeleri.Oncelik(Gruplar(new GrupSorgusu("34-ESENYURT", "belediye-meclisi", "kale", 2009)), KaynakListesi.Bos, Uretim);

        Assert.Equal(2, hepsi.Bolumler.Count);
        var bolum = Assert.Single(kale.Bolumler);
        Assert.Equal("Dönem 2009–2024", bolum.SayfaAdi);
        Assert.Contains("grup: Kale", kale.FiltreOzeti);
        Assert.Equal("oncelik_34-ESENYURT_belediye-meclisi_kale_2009", kale.DosyaAdi);
        Assert.StartsWith("A: 2009'da", (string)bolum.Satirlar[0].Degerler[7]!);   // cümle sütunu
        Assert.Single(bolum.Notlar);                                               // süzgeçte sıra numarası açıklaması
    }

    [Fact]
    public void Meclis_DogrulanmamisUyeUyarisi_DspVurgulu_EsikBolumu()
    {
        var b = RaporBelgeleri.Meclis(Meclis(60_000), KaynakListesi.Bos, Uretim);

        Assert.StartsWith("2024 için 45 üye varsayımdır, YSK sonucundan doğrulanmadı", b.Uyari);
        Assert.Equal(["Dağılım", "DSP için ek oy"], b.Bolumler.Select(x => x.SayfaAdi));
        var d = b.Bolumler[0];
        Assert.Equal("+60.000 oyla üye", d.Sutunlar[^1].Baslik);
        var dsp = d.Satirlar.Single(r => (string)r.Degerler[0]! == "DSP");
        Assert.Equal((SatirTuru.Vurgu, 0, 1), (dsp.Tur, (int)dsp.Degerler[7]!, (int)dsp.Degerler[8]!));
        Assert.Equal((28, 28), ((int)d.Satirlar[0].Degerler[7]!, (int)d.Satirlar[0].Degerler[8]!));
        Assert.Equal(["Barajı geçmek", "1 üye", "2 üye", "3 üye"], b.Bolumler[1].Satirlar.Select(r => (string)r.Degerler[0]!));
        Assert.Equal("meclis_34-ESENYURT_2024_45_ek60000", b.DosyaAdi);
    }

    [Fact]
    public void Excel_UyariSatiri_VurguSatiriSuzgeciBozmaz()
    {
        using var k = new XLWorkbook(new MemoryStream(ExcelYazici.Yaz(RaporBelgeleri.Meclis(Meclis(), KaynakListesi.Bos, Uretim))));

        var bilgi = k.Worksheet("Bilgi");
        var uyari = bilgi.CellsUsed().First(c => c.GetString() == "Uyarı");
        Assert.StartsWith("2024 için 45 üye varsayımdır", uyari.CellRight().GetString());

        var s = k.Worksheet("Dağılım");
        var son = s.LastRowUsed()!.RowNumber();
        Assert.Equal($"A4:H{son}", s.AutoFilter.Range.RangeAddress.ToString()); // DSP satırı (vurgu) süzgecin içinde
        var dspSatiri = s.RowsUsed().First(r => r.Cell(1).GetString() == "DSP");
        Assert.True(dspSatiri.Cell(1).Style.Font.Bold);
    }

    [Fact]
    public async Task Pdf_UyariKutusuVeNoktaliGrafik()
    {
        var meclis = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(RaporBelgeleri.Meclis(Meclis(), KaynakListesi.Bos, Uretim)));
        var gruplar = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(RaporBelgeleri.Gruplar(Gruplar(), KaynakListesi.Bos, Uretim)));

        var kutu = meclis.IndexOf("<div class=\"uyari-kutusu\"><b>Uyarı:</b> 2024 için 45 üye varsayımdır", StringComparison.Ordinal);
        Assert.True(kutu > 0 && kutu < meclis.IndexOf("<table class=\"veri\"", StringComparison.Ordinal), "uyarı tablodan önce");
        Assert.Matches(new Regex(@"<tr class=""toplam"">\s*<td[^>]*>DSP</td>"), meclis);   // DSP satırı kalın
        Assert.Matches(new Regex("<svg class=\"grafik\"[^>]*aria-label=\"Bugünkü güç ve eğilim"), gruplar);
        Assert.DoesNotContain("uyari-kutusu\">", gruplar); // gruplar raporunda uyarı yok
    }
}
