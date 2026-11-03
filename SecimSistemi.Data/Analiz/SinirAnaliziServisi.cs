using Microsoft.EntityFrameworkCore;
using SecimSistemi.Domain;
using SecimSistemi.Domain.Analiz;

namespace SecimSistemi.Data.Analiz;

/// <summary>
/// Bir ilçenin sınır analizini hazırlar: veritabanından birim/mahalle meclis toplamlarını, mahallelerin ilk görüldüğü
/// seçimleri ve geçerli insan kararlarını okur; hesabı <see cref="SinirAnalizi"/>'ne bırakır.
/// </summary>
public sealed class SinirAnaliziServisi(SecimDbContext db)
{
    /// <param name="birimDuzeyinde">true: karşılaştırma birimleri (onaylı eşlemeler birleştirilmiş). false: her mahalle ayrı
    /// (eşik doğrulamasında otomatik tespiti eşlemelerden bağımsız görmek için).</param>
    public async Task<SinirAnaliziSonucu> HazirlaAsync(int ilceId, SinirEsikleri esikler, bool birimDuzeyinde = true, CancellationToken ct = default)
    {
        var mahalleOylari = await db.Sonuclar.AsNoTracking()
            .Where(r => r.IlceId == ilceId && r.OyTuru == OyTuru.BelediyeMeclisi && r.MahalleKoyId != null && r.Secim.Tur == SecimTuru.Yerel)
            .GroupBy(r => new { MahalleKoyId = r.MahalleKoyId!.Value, r.Secim.Tarih })
            .Select(g => new { g.Key.MahalleKoyId, g.Key.Tarih, Oy = g.Sum(x => (long)x.Oy) })
            .ToListAsync(ct);

        // Mahallenin bir seçimde var olması = o seçimde sandığı olması (bütün oy türleri aynı sandıkları kullanır).
        var mahalleYillari = await db.Sandiklar.AsNoTracking()
            .Where(s => s.MahalleKoy.IlceId == ilceId && s.Secim.Tur == SecimTuru.Yerel)
            .Select(s => new { s.MahalleKoyId, s.Secim.Tarih })
            .Distinct()
            .ToListAsync(ct);

        var mahalleler = await db.MahalleKoyler.AsNoTracking()
            .Where(m => m.IlceId == ilceId)
            .Select(m => new { m.Id, m.Kod, m.Ad })
            .ToDictionaryAsync(m => m.Id, ct);

        // Mahalle → birim (kod, ad).
        Dictionary<int, (int Kod, string Ad)> birimi;
        if (birimDuzeyinde)
        {
            birimi = (await db.KarsilastirmaBirimiUyeleri.AsNoTracking()
                    .Where(u => u.Birim.IlceId == ilceId)
                    .Select(u => new { u.MahalleKoyId, u.Birim.Kod, u.Birim.Ad })
                    .ToListAsync(ct))
                .ToDictionary(u => u.MahalleKoyId, u => (u.Kod, u.Ad));
            foreach (var m in mahalleler.Values.Where(m => !birimi.ContainsKey(m.Id)))
                birimi[m.Id] = (m.Kod, m.Ad); // henüz birimi kurulmamış mahalle kendi birimidir
        }
        else
        {
            birimi = mahalleler.Values.ToDictionary(m => m.Id, m => (m.Kod, m.Ad));
        }

        var yillar = mahalleYillari.Select(x => x.Tarih.Year).Distinct().Order().ToList();
        var ilkYillar = mahalleYillari.GroupBy(x => x.MahalleKoyId).ToDictionary(g => g.Key, g => g.Min(x => x.Tarih.Year));
        var yeniMahalleYillari = ilkYillar.Values.Where(y => yillar.Count > 0 && y > yillar[0]).ToHashSet();

        var birimler = birimi
            .GroupBy(kv => kv.Value.Kod)
            .Select(g =>
            {
                var uyeler = g.Select(kv => kv.Key).ToList();
                var oylar = mahalleOylari.Where(o => uyeler.Contains(o.MahalleKoyId))
                    .GroupBy(o => o.Tarih.Year).ToDictionary(x => x.Key, x => x.Sum(o => o.Oy));
                var uyeIlkYillari = uyeler.Where(ilkYillar.ContainsKey).Select(u => ilkYillar[u]).ToList();
                // Birden çok üyeli birim yalnızca onaylı eşlemeyle oluşur: ilk yıl dışındaki üye ilk yılları eşlemeyle katılımdır.
                var katilim = uyeler.Count > 1 && uyeIlkYillari.Count > 0
                    ? uyeIlkYillari.Where(y => y > uyeIlkYillari.Min()).ToHashSet()
                    : new HashSet<int>();
                return new SinirBirimi(g.Key, g.First().Value.Ad, oylar, katilim);
            })
            .ToList();

        var kararlar = await GecerliDurumKararlariAsync(ilceId, ct);
        var sinirKararlari = kararlar
            .Where(k => birimi.ContainsKey(k.MahalleKoyId) && k.Secim is not null)
            .OrderBy(k => k.KararZamani)
            .Select(k => new SinirKarari(birimi[k.MahalleKoyId].Kod, k.Secim!.Tarih.Year,
                k.Karar == DurumKarariTuru.Sabit ? SinirDurumu.Sabit : SinirDurumu.SiniriDegisti,
                $"karar #{k.Id}: {k.Gerekce}"))
            .ToList();

        return SinirAnalizi.Hesapla(new SinirGirdisi(yillar, birimler, yeniMahalleYillari, sinirKararlari), esikler);
    }

    /// <summary>İlçedeki geçerli (onaylı, yerine başkası geçmemiş) Sabit ve SiniriDegisti kararları.</summary>
    public async Task<List<MahalleDurumKarari>> GecerliDurumKararlariAsync(int ilceId, CancellationToken ct = default) =>
        await db.MahalleDurumKararlari.AsNoTracking()
            .Include(k => k.Secim)
            .Include(k => k.MahalleKoy)
            .Where(k => k.MahalleKoy.IlceId == ilceId && k.Durum == KararDurumu.Onaylandi && k.YerineGecenKararId == null
                        && k.Karar != DurumKarariTuru.Esleme)
            .ToListAsync(ct);
}
