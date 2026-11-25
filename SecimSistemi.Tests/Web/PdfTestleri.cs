using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SecimSistemi.Web.Raporlar;

namespace SecimSistemi.Tests.Web;

/// <summary>
/// PDF: şablonun başlık bilgisi (ibare, filtre, veri kaynağı, üretim tarihi), hücre biçimleri ve Chromium ile gerçek üretim.
/// Gerçek üretim testi Chromium'un kurulu olmasını ister (CLAUDE.md'deki kurulum komutu).
/// </summary>
public sealed class PdfTestleri : IAsyncDisposable
{
    private readonly WebApplication _uygulama;
    private readonly PdfUretici _pdf;

    public PdfTestleri()
    {
        // Uygulamadaki servis kümesinin aynısı (HtmlRenderer, Razor bileşen servislerini ister); veritabanı gerekmez.
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

    private static RaporBelgesi Karne() =>
        RaporBelgeleri.Karne(RaporBelgeleriTestleri.OrnekKarne(), RaporBelgeleriTestleri.Kaynaklar, RaporBelgeleriTestleri.Uretim);

    [Fact]
    public async Task Sablon_BastaIbareFiltreKaynakVeTarih()
    {
        var html = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(Karne()));

        var ibare = html.IndexOf("PARTİYE ÖZEL — dağıtılmaz", StringComparison.Ordinal);
        var tablo = html.IndexOf("<table class=\"veri\"", StringComparison.Ordinal);
        Assert.True(ibare >= 0 && ibare < tablo, "ibare tablodan önce olmalı");
        Assert.Contains("<h1>Mahalle karnesi</h1>", html);
        Assert.Contains("İSTANBUL / ESENYURT · 2024 Yerel Seçimleri (31 Mart 2024) · Belediye meclisi · sıra: DSP oranı azalan", html);
        Assert.Contains("YSK Açık Veri Portalı, sandık düzeyi sonuçlar · 2 dosya · son yükleme 3 Kasım 2026", html);
        Assert.Contains("25 Kasım 2026 14:30 (Türkiye saati)", html);
        Assert.Contains(Metinler.EslemeNotu, html);
        Assert.DoesNotContain("<img", html); // başlıkta logo yok
        Assert.Contains($"background: {Gorunum.VarsayilanRenk}", html);
    }

    [Fact]
    public async Task Sablon_NasilOkunurVeDosyaListesiSonda()
    {
        var html = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(Karne()));

        var tablo = html.IndexOf("<table class=\"veri\"", StringComparison.Ordinal);
        var okuma = html.IndexOf("<h2>Bu rapor nasıl okunur</h2>", StringComparison.Ordinal);
        var notlar = html.IndexOf("<h2>Notlar</h2>", StringComparison.Ordinal);
        var dosyalar = html.IndexOf("<h2>Yüklenen dosyalar</h2>", StringComparison.Ordinal);
        Assert.True(tablo < okuma && okuma < notlar && notlar < dosyalar, "sıra: tablo → nasıl okunur → notlar → dosyalar");
        Assert.True(html.IndexOf("2009_yerel_belediye-meclisi_esenyurt.xlsx", StringComparison.Ordinal) > dosyalar, "dosya adları yalnızca sonda");
        Assert.Contains("<td>Karşılaştırılabilir dönem</td><td>Mahalle sınırlarının aynı kaldığı dönem.</td>", html);
        Assert.Contains(SutunSozlugu.Donem.Ornek!, html);
    }

    [Fact]
    public async Task Sablon_SatirlarBicimliVeSinifli()
    {
        var html = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(Karne()));

        Assert.Matches(new Regex("<tr class=\"uyari\">.*?AKÇABURGAZ MAH\\..*?12.274.*?0,32.*?−0,16.*?Şüpheli \\(2019\\).*?2019 seçiminde", RegexOptions.Singleline), html);
        Assert.Matches(new Regex("<tr class=\"toplam\">.*?İlçe toplamı.*?25.078", RegexOptions.Singleline), html);
        Assert.Contains("<th class=\"sayi\">DSP %</th>", html);
    }

    [Fact]
    public async Task Sablon_GrupSatiriBirlesikHucre()
    {
        var bolum = new TabloBolumu("T", "T", null, [new("Ad"), new("Oy", HucreTuru.Tamsayi)],
            [new(["Dönem 2009–2024 · 1 birim"], SatirTuru.Grup), new(["A", null])], []);
        var b = new RaporBelgesi("R", "F", KaynakListesi.Bos, RaporBelgeleriTestleri.Uretim, [bolum], [], "r");

        var html = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(b));

        Assert.Contains("<tr class=\"grup\"><td colspan=\"2\">Dönem 2009–2024 · 1 birim</td></tr>", html);
        Assert.Contains("<td class=\"sayi\">—</td>", html);
    }

    [Fact]
    public async Task Sablon_GrafikSvgVeDonemDisiHucre()
    {
        var b = RaporBelgeleri.Erime(RaporBelgeleriTestleri.OrnekErime(), KaynakListesi.Bos, RaporBelgeleriTestleri.Uretim);

        var html = System.Net.WebUtility.HtmlDecode(await _pdf.HtmlAsync(b));

        Assert.Equal(3, Regex.Matches(html, "<svg class=\"grafik\"").Count); // ilçe çizgisi + iki dönemin en çok kaybedenleri
        Assert.Contains("<h3>ESENYURT · yıllara göre DSP oranı</h3>", html);
        Assert.Contains("<td class=\"donem-disi\"></td>", html);
    }

    [Theory]
    [InlineData(HucreTuru.Tamsayi, 12274L, "12.274")]
    [InlineData(HucreTuru.Tamsayi, 3347.51344, "3.348")]
    [InlineData(HucreTuru.Oran, 0.3177, "0,32")]
    [InlineData(HucreTuru.Puan, -3.5363, "−3,54")]
    [InlineData(HucreTuru.Puan, 0.16, "+0,16")]
    [InlineData(HucreTuru.Katsayi, 0.0824, "0,08")]
    [InlineData(HucreTuru.Kod, 119318, "119318")]
    [InlineData(HucreTuru.Oran, null, "—")]
    [InlineData(HucreTuru.Metin, null, "")]
    public void HucreMetni_EkrandakiBicim(HucreTuru tur, object? deger, string beklenen) =>
        Assert.Equal(beklenen, HucreMetni.Yaz(tur, deger is double d ? (decimal)d : deger));

    [Fact]
    public async Task GercekPdf_A4Yatay()
    {
        var pdf = await _pdf.UretAsync(Karne());

        var metin = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-", metin);
        Assert.Matches(@"/Count [23]\b", metin); // tablo + "Bu rapor nasıl okunur", notlar ve dosya listesi
        Assert.Matches(@"/MediaBox \[0 0 84\d(\.\d+)? 59\d(\.\d+)?\]", metin); // A4 yatay: 842 × 595 pt
    }
}
