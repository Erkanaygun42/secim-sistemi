using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Data.Analiz;

public sealed record IlceSecenegi(int Id, string Kod, string Ad, string IlAdi);

public sealed record SecimSecenegi(int Id, int Yil, DateOnly Tarih, string Ad, SecimTuru Tur);

/// <summary>Karnede bir birimin bir seçim ve oy türündeki satırı.</summary>
/// <param name="Sinir">Birimin yalnızca o seçimdeki sınır tespiti; birim sınır analizinde yoksa (son seçimde yok ya da seçim yerel değil) boş.</param>
/// <param name="DonemSiniri">O seçime göre karşılaştırılabilir dönemin sınır özeti (<see cref="BirimDonemi.DonemSiniri"/>); ekranda bu gösterilir.</param>
/// <param name="OncekiYil">Puan farkının karşılaştırıldığı önceki yerel seçim; birim o seçimde yoksa boş.</param>
/// <param name="PuanFarki">DSP oranının önceki yerel seçime göre farkı, yüzde puan (SecimDegisimi görünümü).</param>
/// <param name="GuncelDonemBaslangici">Birimin bugünkü (son seçime göre) karşılaştırılabilir döneminin başı. Seçilen yıldan sonraysa
/// birimin bugünkü sınırı o yılki alanı göstermez (haritada dönem dışı).</param>
public sealed record KarneSatiri(
    int BirimKod, string BirimAdi, int SandikSayisi, long ToplamOy, bool PusuladaMi, long? DspOyu, decimal? DspOrani,
    BirimYilDurumu? Sinir, DonemSiniri? DonemSiniri, int? OncekiYil = null, decimal? PuanFarki = null, int? GuncelDonemBaslangici = null)
{
    public int? DonemBaslangici => DonemSiniri?.DonemBaslangici;

    /// <summary>O seçime göre dönem içindeki şüpheli seçimler; doluysa satırda uyarı gösterilir.</summary>
    public IReadOnlyList<BirimYilDurumu> Supheliler => DonemSiniri is { UyariVar: true } d ? d.Ilgili : [];

    public bool Uyari => DonemSiniri?.UyariVar == true;

    /// <summary>Sınır tam bu seçimde değiştiği için puan farkı aynı toprağı karşılaştırmıyor.</summary>
    public bool PuanKesintili => PuanFarki is not null && DonemSiniri?.BuSecimdeKesildi == true;
}

public sealed record Karne(IlceSecenegi Ilce, SecimSecenegi Secim, OyTuru OyTuru, SinirEsikleri Esikler, IReadOnlyList<KarneSatiri> Satirlar);

/// <summary>Mahalle detayında bir seçim × oy türü satırı.</summary>
/// <param name="PuanFarki">Önceki yerel seçime göre oran farkı (yüzde puan), SecimDegisimi görünümünden.</param>
public sealed record MahalleSecimSatiri(
    int SecimId, int Yil, OyTuru OyTuru, int SandikSayisi, long ToplamOy, bool PusuladaMi, long? DspOyu, decimal? DspOrani,
    int? OncekiYil, decimal? PuanFarki, BirimYilDurumu? Sinir, DonemSiniri? DonemSiniri = null)
{
    /// <summary>Sınır tam bu seçimde değiştiği için puan farkı aynı toprağı karşılaştırmıyor.</summary>
    public bool PuanKesintili => PuanFarki is not null && DonemSiniri?.BuSecimdeKesildi == true;
}

public sealed record MahalleDetayi(IlceSecenegi Ilce, int BirimKod, string BirimAdi, BirimDonemi? Sinir, SinirEsikleri Esikler,
    IReadOnlyList<MahalleSecimSatiri> Satirlar);

/// <summary>
/// Mahalle karnesi ve mahalle detayı için veri: MahalleKarnesi ve SecimDegisimi görünümlerini okur, her satıra
/// <see cref="SinirAnaliziServisi"/>'nden birimin sınır durumunu ekler. Sınır eşikleri sayfalarda sabittir (K16).
/// </summary>
public sealed class KarneServisi(SecimDbContext db)
{
    public static readonly SinirEsikleri Esikler = SinirEsikleri.Varsayilan;

    /// <summary>Karnede verisi olan ilçeler, il ve ilçe adına göre.</summary>
    public async Task<List<IlceSecenegi>> IlcelerAsync(CancellationToken ct = default)
    {
        var idler = db.MahalleKarnesi.Select(k => k.IlceId).Distinct();
        var liste = await db.Ilceler.AsNoTracking()
            .Where(i => idler.Contains(i.Id))
            .Select(i => new IlceSecenegi(i.Id, i.Kod, i.Ad, i.Il.Ad))
            .ToListAsync(ct);
        return liste.OrderBy(i => i.IlAdi, Siralama.Turkce).ThenBy(i => i.Ad, Siralama.Turkce).ToList();
    }

    public async Task<IlceSecenegi?> IlceAsync(string kod, CancellationToken ct = default) =>
        await db.Ilceler.AsNoTracking()
            .Where(i => i.Kod == kod)
            .Select(i => new IlceSecenegi(i.Id, i.Kod, i.Ad, i.Il.Ad))
            .SingleOrDefaultAsync(ct);

    /// <summary>İlçede verisi olan seçimler, en yeni önce.</summary>
    public async Task<List<SecimSecenegi>> SecimlerAsync(int ilceId, CancellationToken ct = default)
    {
        var idler = db.MahalleKarnesi.Where(k => k.IlceId == ilceId).Select(k => k.SecimId).Distinct();
        return await db.Secimler.AsNoTracking()
            .Where(s => idler.Contains(s.Id))
            .OrderByDescending(s => s.Tarih)
            .Select(s => new SecimSecenegi(s.Id, s.Tarih.Year, s.Tarih, s.Ad, s.Tur))
            .ToListAsync(ct);
    }

    /// <summary>İlçede o seçimde verisi olan oy türleri, enum sırasıyla.</summary>
    public async Task<List<OyTuru>> OyTurleriAsync(int ilceId, int secimId, CancellationToken ct = default) =>
        // Oy türü veritabanında metin olarak tutulur; enum sırası için sıralama okunduktan sonra yapılır.
        (await db.MahalleKarnesi.AsNoTracking()
            .Where(k => k.IlceId == ilceId && k.SecimId == secimId)
            .Select(k => k.OyTuru).Distinct()
            .ToListAsync(ct))
        .Order().ToList();

    /// <summary>Karne satırları; varsayılan sıra DSP oranı azalan.</summary>
    public async Task<Karne> KarneAsync(IlceSecenegi ilce, SecimSecenegi secim, OyTuru oyTuru, CancellationToken ct = default)
    {
        var satirlar = await db.MahalleKarnesi.AsNoTracking()
            .Where(k => k.IlceId == ilce.Id && k.SecimId == secim.Id && k.OyTuru == oyTuru)
            .ToListAsync(ct);

        var degisim = (await db.SecimDegisimi.AsNoTracking()
                .Where(d => d.IlceId == ilce.Id && d.SecimId == secim.Id && d.OyTuru == oyTuru)
                .Select(d => new { d.BirimKod, d.OncekiYil, d.PuanFarki })
                .ToListAsync(ct))
            .ToDictionary(d => d.BirimKod);

        var sinir = await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, Esikler, ct: ct);

        var sonuc = satirlar.Select(k =>
        {
            var birim = sinir.Birim(k.BirimKod);
            var d = degisim.GetValueOrDefault(k.BirimKod);
            return new KarneSatiri(k.BirimKod, k.BirimAdi, k.SandikSayisi, k.ToplamOy, k.PusuladaMi, k.DspOyu, k.DspOrani,
                birim?.Durum(secim.Yil), birim?.DonemSiniri(secim.Yil), d?.OncekiYil, d?.PuanFarki, birim?.DonemBaslangici);
        });

        return new Karne(ilce, secim, oyTuru, Esikler, KarneSiralamasi.Uygula(sonuc, KarneSiraAlani.DspOrani, azalan: true));
    }

    /// <summary>Bir birimin bütün seçimleri ve oy türleri, en yeni seçim önce; birim yoksa boş.</summary>
    public async Task<MahalleDetayi?> MahalleAsync(IlceSecenegi ilce, int birimKod, CancellationToken ct = default)
    {
        var karne = await db.MahalleKarnesi.AsNoTracking()
            .Where(k => k.IlceId == ilce.Id && k.BirimKod == birimKod)
            .ToListAsync(ct);
        if (karne.Count == 0) return null;

        var degisim = (await db.SecimDegisimi.AsNoTracking()
                .Where(d => d.IlceId == ilce.Id && d.BirimKod == birimKod)
                .ToListAsync(ct))
            .ToDictionary(d => (d.SecimId, d.OyTuru));

        var sinir = (await new SinirAnaliziServisi(db).HazirlaAsync(ilce.Id, Esikler, ct: ct)).Birim(birimKod);

        var satirlar = karne
            .OrderByDescending(k => k.Tarih).ThenBy(k => k.OyTuru)
            .Select(k =>
            {
                var d = degisim.GetValueOrDefault((k.SecimId, k.OyTuru));
                return new MahalleSecimSatiri(k.SecimId, k.Yil, k.OyTuru, k.SandikSayisi, k.ToplamOy, k.PusuladaMi, k.DspOyu, k.DspOrani,
                    d?.OncekiYil, d?.PuanFarki, sinir?.Durum(k.Yil), sinir?.DonemSiniri(k.Yil));
            })
            .ToList();

        // Ad, en son seçimdeki birim adıdır.
        var ad = karne.MaxBy(k => k.Tarih)!.BirimAdi;
        return new MahalleDetayi(ilce, birimKod, ad, sinir, Esikler, satirlar);
    }
}
