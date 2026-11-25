using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Data.Analiz;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Web.Raporlar;

/// <summary>
/// Meclis hesaplayıcısının adresteki filtreleri:
/// <c>?ilce=34-ESENYURT&amp;secim=4&amp;uye=45&amp;ek-oy=50000&amp;kaynak=yeni-secmen</c>.
/// <c>uye</c> verilmezse tanım dosyasındaki sayı; <c>kaynak</c>: <c>yeni-secmen</c> (varsayılan) ya da <c>en-buyuk-parti</c>.
/// </summary>
public sealed record MeclisSorgusu(string? Ilce, int? Secim, int? Uye, long? EkOy, string? Kaynak)
{
    public const string YeniSecmen = "yeni-secmen", EnBuyukParti = "en-buyuk-parti";

    public EkOyKaynagi KaynakSecimi => Kaynak == EnBuyukParti ? EkOyKaynagi.EnBuyukParti : EkOyKaynagi.YeniSecmen;

    public static MeclisSorgusu Oku(IQueryCollection q) =>
        new(q["ilce"], int.TryParse(q["secim"], out var s) ? s : null, int.TryParse(q["uye"], out var u) ? u : null,
            long.TryParse(q["ek-oy"], out var e) ? e : null, q["kaynak"]);

    public Dictionary<string, object?> Parametreler() => new()
    {
        ["ilce"] = Ilce, ["secim"] = Secim, ["uye"] = Uye, ["ek-oy"] = EkOy, ["kaynak"] = Kaynak,
    };

    public string SorguMetni() =>
        string.Join("&", Parametreler().Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, Metinler.Turkce)!)}"));
}

/// <summary>Uygulamanın okuduğu meclis üye sayıları (<c>Tanimlar/meclis-uye-sayilari.csv</c>).</summary>
public sealed record MeclisUyeTanimlari(IReadOnlyList<MeclisUyeSayisi> Kayitlar)
{
    /// <summary>Uygulamanın çıktı klasöründeki tanım dosyası (Web projesi Importer'daki dosyayı kopyalar); yoksa boş.</summary>
    public static MeclisUyeTanimlari Yukle()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "Tanimlar", MeclisUyeSayilari.DosyaAdi);
        return new(File.Exists(yol) ? MeclisUyeSayilari.Oku(yol) : []);
    }
}

/// <summary>Kullanılan üye sayısının dayanağı.</summary>
public enum UyeSayisiKaynagi
{
    /// <summary>Tanım dosyasında, YSK sonucundan okundu.</summary>
    Dogrulandi,
    /// <summary>Tanım dosyasında, ama varsayım.</summary>
    Dogrulanmadi,
    /// <summary>Tanım dosyasında bu yıl yok; ilçenin başka yılından ya da varsayılandan alındı.</summary>
    Tahmin,
    /// <summary>Kullanıcı sayfada seçti.</summary>
    Secildi,
}

/// <param name="Mevcut">Seçim günkü oylarla dağılım.</param>
/// <param name="Senaryo">DSP'ye ek oy eklenmiş dağılım; ek oy 0 ya da DSP yoksa boş.</param>
/// <param name="Esikler">DSP'nin bir sonraki üyelikleri için gereken ek oy (seçili kaynakla).</param>
/// <param name="Hata">Senaryo hesaplanamadıysa nedeni (örnek: en büyük partinin oyundan fazla).</param>
public sealed record MeclisSonuclari(
    IReadOnlyList<IlceSecenegi> Ilceler, IlceSecenegi? Ilce, IReadOnlyList<SecimSecenegi> Secimler, SecimSecenegi? Secim,
    int UyeSayisi, UyeSayisiKaynagi UyeKaynagi, MeclisUyeSayisi? Tanim,
    IReadOnlyList<MeclisListesi> Listeler, MeclisSonucu? Mevcut, MeclisSonucu? Senaryo, long EkOy, EkOyKaynagi Kaynak,
    long? BarajIcin, IReadOnlyList<KoltukEsigi> Esikler, string? Hata, MeclisSorgusu Sorgu)
{
    public const string Hedef = "DSP";

    public bool DspVar => Listeler.Any(l => l.Ad == Hedef);

    public bool UyeDogrulandi => UyeKaynagi == UyeSayisiKaynagi.Dogrulandi;

    /// <summary>Sayfada ve dosyada üye sayısının yanında gösterilen uyarı; doğrulandıysa boş.</summary>
    public string? UyeUyarisi => Secim is null ? null : UyeKaynagi switch
    {
        UyeSayisiKaynagi.Dogrulandi => null,
        UyeSayisiKaynagi.Dogrulanmadi =>
            $"{Secim.Yil} için {UyeSayisi} üye varsayımdır, YSK sonucundan doğrulanmadı ({Tanim?.Kaynak}). Dağılım bu sayıya bağlıdır.",
        UyeSayisiKaynagi.Tahmin =>
            $"{Secim.Yil} için meclis üye sayısı tanım dosyasında yok; {UyeSayisi} üyeyle hesaplandı (doğrulanmadı). " +
            "Gerçek üye sayısı farklıysa dağılım da farklıdır.",
        _ => $"Üye sayısı sayfada {UyeSayisi} olarak seçildi" +
             (Tanim is { } t ? $"; tanım dosyasındaki sayı {t.UyeSayisi} ({(t.Dogrulandi ? "doğrulandı" : "doğrulanmadı")})." : "; tanım dosyasında bu yıl için sayı yok."),
    };

    public string FiltreOzeti => Ilce is null || Secim is null ? "" :
        $"{Ilce.IlAdi} / {Ilce.Ad} · {Secim.Ad} · belediye meclisi · {UyeSayisi} üye ({(UyeDogrulandi ? "doğrulandı" : "doğrulanmadı")})" +
        (EkOy > 0 ? $" · DSP'ye +{Metinler.Oy(EkOy)} oy ({(Kaynak == EkOyKaynagi.YeniSecmen ? "yeni seçmenden" : "en büyük partiden")})" : "");
}

/// <summary>Meclis hesaplayıcısını hazırlar: oylar veritabanından, üye sayısı tanım dosyasından (ya da sayfadan).</summary>
public sealed class MeclisHazirlayici(IDbContextFactory<SecimDbContext> fabrika, MeclisUyeTanimlari tanimlar)
{
    /// <summary>Tanım dosyasında olmayan ilçe için varsayılan (Esenyurt büyüklüğünde bir ilçe; doğrulanmadı olarak gösterilir).</summary>
    public const int VarsayilanUye = 45;

    public const int GosterilecekEsik = 3;

    public static readonly int[] KanundakiUyeSayilari = [9, 11, 15, 25, 31, 37, 45, 55];

    public async Task<MeclisSonuclari> HazirlaAsync(MeclisSorgusu sorgu, CancellationToken ct = default)
    {
        await using var db = await fabrika.CreateDbContextAsync(ct);
        var karne = new KarneServisi(db);
        var ilceler = await karne.IlcelerAsync(ct);
        var ilce = ilceler.FirstOrDefault(i => i.Kod == sorgu.Ilce) ?? ilceler.FirstOrDefault();
        var secimler = ilce is null ? [] : (await karne.SecimlerAsync(ilce.Id, ct)).Where(s => s.Tur == SecimTuru.Yerel).ToList();
        var secim = secimler.FirstOrDefault(s => s.Id == sorgu.Secim) ?? secimler.FirstOrDefault();
        var listeler = ilce is null || secim is null ? [] : await new MeclisServisi(db).ListelerAsync(ilce.Id, secim.Id, ct);
        return Hesapla(ilceler, ilce, secimler, secim, listeler, sorgu, tanimlar);
    }

    /// <summary>Veritabanından bağımsız kısım (test edilir).</summary>
    public static MeclisSonuclari Hesapla(IReadOnlyList<IlceSecenegi> ilceler, IlceSecenegi? ilce, IReadOnlyList<SecimSecenegi> secimler,
        SecimSecenegi? secim, IReadOnlyList<MeclisListesi> listeler, MeclisSorgusu sorgu, MeclisUyeTanimlari tanimlar)
    {
        var tanim = ilce is null || secim is null ? null : tanimlar.Kayitlar.FirstOrDefault(t => t.IlceKod == ilce.Kod && t.Yil == secim.Yil);
        var (uye, uyeKaynagi) =
            sorgu.Uye is { } u && KanundakiUyeSayilari.Contains(u) && u != tanim?.UyeSayisi ? (u, UyeSayisiKaynagi.Secildi)
            : tanim is { } t ? (t.UyeSayisi, t.Dogrulandi ? UyeSayisiKaynagi.Dogrulandi : UyeSayisiKaynagi.Dogrulanmadi)
            : (ilce is null ? VarsayilanUye : tanimlar.Kayitlar.Where(x => x.IlceKod == ilce.Kod).OrderByDescending(x => x.Yil).FirstOrDefault()?.UyeSayisi ?? VarsayilanUye,
               UyeSayisiKaynagi.Tahmin);

        var kaynak = sorgu.KaynakSecimi;
        var ekOy = Math.Max(0, sorgu.EkOy ?? 0);
        var cozulmus = new MeclisSorgusu(ilce?.Kod, secim?.Id, uyeKaynagi == UyeSayisiKaynagi.Secildi ? uye : null,
            ekOy == 0 ? null : ekOy, kaynak == EkOyKaynagi.EnBuyukParti ? MeclisSorgusu.EnBuyukParti : null);

        if (listeler.Count == 0)
            return new MeclisSonuclari(ilceler, ilce, secimler, secim, uye, uyeKaynagi, tanim, listeler, null, null, ekOy, kaynak, null, [], null, cozulmus);

        var mevcut = MeclisHesabi.Dagit(listeler, uye);
        if (listeler.All(l => l.Ad != MeclisSonuclari.Hedef))
            return new MeclisSonuclari(ilceler, ilce, secimler, secim, uye, uyeKaynagi, tanim, listeler, mevcut, null, ekOy, kaynak, null, [],
                $"{secim!.Yil} belediye meclisi seçiminde DSP'nin listesi yok; ek oy hesaplanamaz.", cozulmus);

        MeclisSonucu? senaryo = null;
        string? hata = null;
        if (ekOy > 0)
        {
            try { senaryo = MeclisHesabi.Dagit(MeclisHesabi.EkOyEkle(listeler, MeclisSonuclari.Hedef, ekOy, kaynak), uye); }
            catch (ArgumentException)
            {
                var enBuyuk = MeclisHesabi.EnBuyukDiger(listeler, MeclisSonuclari.Hedef);
                hata = $"Ek oy ({Metinler.Oy(ekOy)}) en büyük partinin ({enBuyuk?.Ad}) oyundan ({Metinler.Oy(enBuyuk?.Oy)}) fazla.";
            }
        }

        var mevcutKoltuk = mevcut.Koltuk(MeclisSonuclari.Hedef);
        var esikler = MeclisHesabi.KoltukEsikleri(listeler, uye, MeclisSonuclari.Hedef, kaynak, mevcutKoltuk + GosterilecekEsik);
        var baraj = MeclisHesabi.BarajIcinGerekenOy(listeler, uye, MeclisSonuclari.Hedef, kaynak);
        return new MeclisSonuclari(ilceler, ilce, secimler, secim, uye, uyeKaynagi, tanim, listeler, mevcut, senaryo, ekOy, kaynak,
            baraj, esikler, hata, cozulmus);
    }
}
