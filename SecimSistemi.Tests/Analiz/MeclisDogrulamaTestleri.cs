using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Tests.Analiz;

/// <summary>
/// Meclis hesaplayıcısının gerçek seçimlerle doğrulanması (docs/kararlar.md K36, K40).
/// Oylar: Esenyurt belediye meclisi sandık sonuçlarının toplamı (YSK Açık Veri Portalı; veritabanımızdan 2026-11-25'te
/// alındı). Bağımsız adayların adları yazılmadı; hesapta yalnızca oyları ve bağımsız olmaları önemlidir.
/// <para>
/// Beklenen dağılımlar şimdilik <b>resmî değil, basın</b> kaynaklıdır (Trait "Kaynak" = "basın"). Basın sayıları seçimden
/// sonraki parti değişikliklerini içerebilir ve liste içindeki partileri ayrı sayar (ör. AK Parti listesinden seçilen MHP'liler);
/// hesap listeyi sayar. YSK'nın ilan ettiği resmî dağılım gelince bu testler onunla değiştirilecek.
/// </para>
/// </summary>
public class MeclisDogrulamaTestleri
{
    private static MeclisListesi L(string ad, long oy, bool bagimsiz = false) => new(ad, oy, bagimsiz);

    /// <summary>2019 Esenyurt belediye meclisi: 10 liste, geçerli oy 436.869.</summary>
    private static readonly MeclisListesi[] Esenyurt2019 =
    [
        L("AK PARTİ", 198886), L("CHP", 175807), L("HDP", 48567), L("SAADET", 5216), L("BBP", 3772), L("DSP", 1353),
        L("DP", 1126), L("BTP", 912), L("TKP", 671), L("VATAN PARTİSİ", 559),
    ];

    /// <summary>2024 Esenyurt belediye meclisi: 23 liste (2'si bağımsız), geçerli oy 463.488.</summary>
    private static readonly MeclisListesi[] Esenyurt2024 =
    [
        L("CHP", 221877), L("AK PARTİ", 176194), L("ZAFER PARTİSİ", 18220), L("YENİDEN REFAH", 16196), L("İYİ PARTİ", 6385),
        L("SAADET", 3003), L("MEMLEKET", 2944), L("SOL PARTİ", 2856), L("TİP", 2752), L("DEVA PARTİSİ", 2737), L("HÜDA PAR", 2004),
        L("BTP", 1416), L("GELECEK PARTİSİ", 1257), L("DSP", 975), L("EMEP", 952), L("TKP", 879), L("BAĞIMSIZ 1", 878, true),
        L("BAĞIMSIZ 2", 511, true), L("VATAN PARTİSİ", 460), L("ABP", 293), L("HKP", 242), L("AB PARTİ", 231), L("MİLLET", 226),
    ];

    /// <summary>
    /// Basın, resmî değil: AK Parti listesi 27 (25 AK Parti + 2 MHP), CHP listesi 18 (16 CHP + 2 İYİ Parti), HDP 0.
    /// Kaynak (kullanıcının ilettiği): https://www.sozcu.com.tr/akp-mhp-grubu-ile-chp-iyi-parti-grubu-arasinda-oylama-krizi-wp6736514
    /// HDP %11,12 ile barajı geçer ama 0 üye alır: barajın üstünde yalnızca 4.880 oy kalır (kalan oyla bölme, 2972 m.23/a).
    /// </summary>
    [Fact]
    [Trait("Kaynak", "basın")]
    public void Esenyurt2019_45Uye_BasinKaynakli()
    {
        var s = MeclisHesabi.Dagit(Esenyurt2019, 45);

        Assert.Equal(27, s.Koltuk("AK PARTİ"));
        Assert.Equal(18, s.Koltuk("CHP"));
        Assert.Equal(0, s.Koltuk("HDP"));
        Assert.True(s.Liste("HDP")!.BarajiGecti);
        Assert.Equal("AK PARTİ", s.KontenjanAlan);
        Assert.Empty(s.KuraListeleri);
    }

    /// <summary>
    /// Basın, resmî değil: CHP 28, AK Parti listesi 17 (14 AK Parti + 3 MHP).
    /// Kaynak (kullanıcının ilettiği): https://www.aydinlik.com.tr/haber/esenyurt-belediyesi-hangi-partiden-kim-yonetiyor-593361
    /// </summary>
    [Fact]
    [Trait("Kaynak", "basın")]
    public void Esenyurt2024_45Uye_BasinKaynakli()
    {
        var s = MeclisHesabi.Dagit(Esenyurt2024, 45);

        Assert.Equal(28, s.Koltuk("CHP"));
        Assert.Equal(17, s.Koltuk("AK PARTİ"));
        Assert.Equal(45, s.Listeler.Sum(l => l.Toplam));
        Assert.Equal("CHP", s.KontenjanAlan);
        Assert.Empty(s.KuraListeleri);
    }

    /// <summary>
    /// 2014: resmî dağılım bekleniyor (kullanıcı YSK'dan iletecek). Kanunun okunuşuna göre HDP %10,23 ile barajı geçer ama
    /// barajın üstünde 792 oy kaldığı için 0 üye almalı (düz D'Hondt ile 4–5 üye alırdı). Üye sayısı da doğrulanmadı.
    /// </summary>
    [Fact(Skip = "2014 resmî dağılımı ve üye sayısı bekleniyor (YSK); HDP'nin üye sayısı bu testin ana konusu.")]
    [Trait("Kaynak", "resmî")]
    public void Esenyurt2014_ResmiDagilim()
    {
    }
}
