using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SecimSistemi.Data;
using SecimSistemi.Domain;
using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Importer.Mahalleler;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Aktarim;

public enum YuklemeIslemi
{
    Yuklendi,
    Yenilendi,
    Atlandi,
}

public sealed record YuklemeOzeti(
    string DosyaAdi,
    YuklemeIslemi Islem,
    int SatirSayisi,
    int SandikSayisi,
    int MahalleSayisi,
    long ToplamOy,
    long? DspOyu,
    KontrolSonucu Kontrol,
    IReadOnlyList<string> Uyarilar);

/// <summary>
/// Bir YSK sandık dosyasını veritabanına yükler. Sıra:
/// 1. SHA256 hesaplanır; aynı içerik daha önce yüklendiyse hiçbir şey yapılmadan atlanır.
/// 2. Dosyanın tamamı okunur ve doğrulanır (hücreler, başlıklar, mahalle türleri). Sorun varsa veritabanına dokunulmaz.
/// 3. Aynı seçim + oy türü + ilçe başka bir dosyayla yüklenmişse, --yenile verilmedikçe durulur.
/// 4. Tek transaction içinde: bölgeler, seçim, partiler, mahalleler, sandıklar, adaylar, sonuçlar, DSP katılımı, yükleme kaydı.
/// 5. Ham dosya arşive kopyalanır, transaction commit edilir.
/// Çağıran zaten bir transaction açtıysa (testler) o kullanılır; commit çağırana kalır.
/// </summary>
public sealed class Yukleyici(SecimDbContext db, BaslikTanimlari tanimlar, string arsivKlasoru)
{
    public const string DspKisaAd = "DSP";

    /// <summary>DSP'nin yer aldığı ittifak, seçim yılına göre.</summary>
    public static readonly IReadOnlyDictionary<int, string> DspIttifaklari = new Dictionary<int, string>
    {
        [2024] = "Cumhur İttifakı",
    };

    private readonly BaslikEslestirici _eslestirici = new(tanimlar);

    public async Task<YuklemeOzeti> YukleAsync(string yol, bool yenile = false, CancellationToken ct = default)
    {
        var kimlik = DosyaKimligi.Coz(yol);
        if (kimlik.KontrolDosyasiMi)
            throw new YuklemeHatasi($"{kimlik.DosyaAdi} bir kontrol dosyası; sandık dosyası gibi yüklenmez.");

        var icerik = await File.ReadAllBytesAsync(yol, ct);
        var sha = Convert.ToHexStringLower(SHA256.HashData(icerik));
        db.ChangeTracker.Clear();

        // 1. Aynı içerik daha önce yüklendi mi?
        var ayniIcerik = await db.Yuklemeler.AsNoTracking().FirstOrDefaultAsync(y => y.Sha256 == sha, ct);
        if (ayniIcerik is not null)
        {
            return new YuklemeOzeti(kimlik.DosyaAdi, YuklemeIslemi.Atlandi, ayniIcerik.SatirSayisi, ayniIcerik.SandikSayisi,
                ayniIcerik.MahalleSayisi, ayniIcerik.ToplamOy, ayniIcerik.DspOyu, ayniIcerik.KontrolSonucu,
                [$"Aynı içerik {ayniIcerik.YuklenmeZamani.ToLocalTime():dd.MM.yyyy HH:mm} tarihinde '{ayniIcerik.DosyaAdi}' adıyla yüklenmiş (SHA256 {sha[..12]}…); atlandı."]);
        }

        // 2. Veritabanına dokunmadan önce dosyanın tamamı okunur ve doğrulanır.
        var dosya = YskDosyaOkuyucu.Oku(new MemoryStream(icerik), kimlik.DosyaAdi);
        var eslesme = _eslestirici.Eslestir(dosya.OySutunlari, kimlik.SecimTarihi, kimlik.OyTuru);
        eslesme.BasarisizsaDur(kimlik.DosyaAdi, kimlik.SecimTarihi, kimlik.OyTuru);
        var mahalleler = MahalleBilgileri(dosya);

        var uyarilar = new List<string>();
        uyarilar.AddRange(mahalleler.Uyarilar);
        uyarilar.AddRange(VeriKalitesi.AyniOyluSatirlar(dosya));

        // 3. Aynı seçim + oy türü + ilçe başka bir dosyayla yüklenmiş mi? (Henüz hiçbir şey yazılmadı.)
        var ilceTanimi = kimlik.Ilce;
        var aktif = await db.Yuklemeler
            .Where(y => y.Durum == YuklemeDurumu.Aktif && y.OyTuru == kimlik.OyTuru
                        && y.Secim.Tarih == kimlik.SecimTarihi && y.Ilce.Kod == ilceTanimi.Kod)
            .FirstOrDefaultAsync(ct);
        if (aktif is not null && !yenile)
            throw new YuklemeHatasi(
                $"{kimlik.DosyaAdi}: {kimlik.Yil} {OyTuruAdlari.Ad(kimlik.OyTuru)} {ilceTanimi.Ad} sonuçları " +
                $"{aktif.YuklenmeZamani.ToLocalTime():dd.MM.yyyy HH:mm} tarihinde başka bir dosyayla yüklenmiş ('{aktif.DosyaAdi}', SHA256 {aktif.Sha256[..12]}…). " +
                "Bu dosyayla değiştirmek için --yenile verin.");

        var kendiTransaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // 4. Yazma.
            var il = await IlAsync(ilceTanimi.Il, ct);
            var ilce = await IlceAsync(ilceTanimi, il, ct);
            var secim = await SecimAsync(kimlik.SecimTarihi, ct);
            var partiler = await PartileriEsitleAsync(ct);

            if (aktif is not null)
                await EskiYuklemeyiKaldirAsync(aktif, ct);

            var mahalleKayitlari = await MahalleleriHazirlaAsync(mahalleler, ilce, uyarilar, ct);
            var sandiklar = await SandiklariHazirlaAsync(dosya, secim, mahalleKayitlari, ct);

            // Büyükşehir adayı il genelinde yarışır; meclis ve belediye başkanı adayı ilçede.
            var adayIlcesi = kimlik.OyTuru == OyTuru.BuyuksehirBaskani ? null : ilce;
            var sutunlar = await SutunKalemleriAsync(eslesme, secim, kimlik.OyTuru, il, adayIlcesi, partiler, ct);

            var sonuclar = new List<Sonuc>(dosya.Satirlar.Count * sutunlar.Count);
            foreach (var satir in dosya.Satirlar)
            {
                var bolge = Bolge.SandikDuzeyinde(sandiklar[(satir.MuhtarlikId, satir.SandikNo)]);
                for (int i = 0; i < sutunlar.Count; i++)
                {
                    var (sira, parti, aday) = sutunlar[i];
                    var oy = satir.Oylar[sira];
                    sonuclar.Add(parti is not null
                        ? Sonuc.PartiOyu(secim, kimlik.OyTuru, bolge, parti, oy)
                        : Sonuc.AdayOyu(secim, kimlik.OyTuru, bolge, aday!, oy));
                }
            }
            db.Sonuclar.AddRange(sonuclar);

            long toplamOy = dosya.Satirlar.Sum(s => s.Oylar.Sum(o => (long)o));
            var dspSirasi = sutunlar.FindIndex(s => s.Parti?.KisaAd == DspKisaAd);
            long? dspOyu = dspSirasi < 0 ? null : dosya.Satirlar.Sum(s => (long)s.Oylar[sutunlar[dspSirasi].Sira]);

            await DspKatiliminiYazAsync(secim, kimlik, il, adayIlcesi, dspOyu, uyarilar, ct);

            var (kontrol, kontrolUyarisi) = KontrolDosyasi(yol, kimlik);
            uyarilar.Add(kontrolUyarisi);

            var mahalleSayisi = mahalleler.Adlar.Count;
            db.Yuklemeler.Add(new Yukleme
            {
                DosyaAdi = kimlik.DosyaAdi,
                Sha256 = sha,
                YuklenmeZamani = DateTimeOffset.UtcNow,
                Secim = secim,
                OyTuru = kimlik.OyTuru,
                Ilce = ilce,
                SatirSayisi = dosya.Satirlar.Count,
                SandikSayisi = sandiklar.Count,
                MahalleSayisi = mahalleSayisi,
                ToplamOy = toplamOy,
                DspOyu = dspOyu,
                KontrolSonucu = kontrol,
                Durum = YuklemeDurumu.Aktif,
            });

            await db.SaveChangesAsync(ct);

            if (aktif is not null)
                await YetimKayitlariSilAsync(secim, ilce, kimlik.OyTuru, ct);

            // Yeni gelen mahallelerin de bir karşılaştırma birimi olsun. Değişiklik izleyicisi boşaltılır:
            // on binlerce kaydedilmiş sonucu yeniden taramaya gerek yok.
            db.ChangeTracker.Clear();
            await BirimKurucu.YenidenKurAsync(db, ilce.Id, ct);

            // 5. Arşiv ve commit.
            Arsivle(icerik, sha);
            if (kendiTransaction is not null)
                await kendiTransaction.CommitAsync(ct);

            return new YuklemeOzeti(kimlik.DosyaAdi, aktif is null ? YuklemeIslemi.Yuklendi : YuklemeIslemi.Yenilendi,
                dosya.Satirlar.Count, sandiklar.Count, mahalleSayisi, toplamOy, dspOyu, kontrol, uyarilar);
        }
        finally
        {
            if (kendiTransaction is not null)
                await kendiTransaction.DisposeAsync(); // commit edilmediyse geri alınır
            db.ChangeTracker.Clear();
        }
    }

    // ---- Dosyadan çıkan, veritabanından bağımsız bilgiler ----

    private sealed record MahalleOkumasi(Dictionary<int, (string Ad, MahalleKoyTuru Tur)> Adlar, List<string> Uyarilar);

    /// <summary>Muhtarlık Id → ad ve tür. Tür addan çıkarılır: "MAH." mahalle, "KÖYÜ" köy; ikisi de değilse durulur.</summary>
    private static MahalleOkumasi MahalleBilgileri(YskSandikDosyasi dosya)
    {
        var adlar = new Dictionary<int, (string Ad, MahalleKoyTuru Tur)>();
        var uyarilar = new List<string>();
        var taninmayan = new SortedSet<string>();

        foreach (var s in dosya.Satirlar)
        {
            if (adlar.TryGetValue(s.MuhtarlikId, out var mevcut))
            {
                if (mevcut.Ad != s.MuhtarlikAdi)
                    uyarilar.Add($"{s.SatirNo}. satır: Muhtarlık Id {s.MuhtarlikId} önceki satırlarda '{mevcut.Ad}', burada '{s.MuhtarlikAdi}'; ilk ad kullanıldı.");
                continue;
            }

            MahalleKoyTuru? tur =
                s.MuhtarlikAdi.EndsWith(" MAH.", StringComparison.Ordinal) || s.MuhtarlikAdi.EndsWith(" MAHALLESİ", StringComparison.Ordinal) ? MahalleKoyTuru.Mahalle :
                s.MuhtarlikAdi.EndsWith(" KÖYÜ", StringComparison.Ordinal) || s.MuhtarlikAdi.EndsWith(" KÖY", StringComparison.Ordinal) ? MahalleKoyTuru.Koy :
                null;
            if (tur is null)
            {
                taninmayan.Add($"{s.MuhtarlikId} '{s.MuhtarlikAdi}'");
                continue;
            }
            adlar[s.MuhtarlikId] = (s.MuhtarlikAdi, tur.Value);
        }

        if (taninmayan.Count > 0)
            throw new YuklemeHatasi($"{dosya.DosyaAdi}: şu birimlerin mahalle mi köy mü olduğu adından anlaşılamadı (\"MAH.\" ya da \"KÖYÜ\" bekleniyordu):{Environment.NewLine}"
                                    + string.Join(Environment.NewLine, taninmayan.Select(t => "  " + t)));
        return new MahalleOkumasi(adlar, uyarilar);
    }

    private static (KontrolSonucu, string) KontrolDosyasi(string yol, DosyaKimligi kimlik)
    {
        var ad = $"{kimlik.Yil}_yerel_{OyTuruAdlari.Ad(kimlik.OyTuru)}_{DosyaKimligi.KontrolDosyasiIlceAdi}.xlsx";
        var kontrolYolu = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(yol))!, ad);
        return File.Exists(kontrolYolu)
            ? (KontrolSonucu.Yapilmadi, $"Kontrol dosyası {ad} bulundu ama okuyucusu henüz yazılmadı; toplam kontrolü yapılmadı.")
            : (KontrolSonucu.DosyaYok, $"Kontrol dosyası {ad} yok; toplam kontrolü yapılmadı.");
    }

    // ---- Veritabanı adımları ----

    private async Task<Il> IlAsync(IlTanimi t, CancellationToken ct) =>
        await db.Iller.FirstOrDefaultAsync(i => i.Kod == t.Kod, ct)
        ?? db.Iller.Add(new Il { Kod = t.Kod, Ad = t.Ad }).Entity;

    private async Task<Ilce> IlceAsync(IlceTanimi t, Il il, CancellationToken ct)
    {
        var ilce = await db.Ilceler.FirstOrDefaultAsync(i => i.Kod == t.Kod, ct);
        if (ilce is null)
            return db.Ilceler.Add(new Ilce { Kod = t.Kod, Ad = t.Ad, Il = il }).Entity;
        if (ilce.IlId != il.Id)
            throw new YuklemeHatasi($"Veritabanındaki '{t.Kod}' ilçesi başka bir ile bağlı.");
        return ilce;
    }

    private async Task<Secim> SecimAsync(DateOnly tarih, CancellationToken ct)
    {
        var secim = await db.Secimler.FirstOrDefaultAsync(s => s.Tarih == tarih, ct);
        if (secim is null)
            return db.Secimler.Add(new Secim { Tarih = tarih, Tur = SecimTuru.Yerel, Ad = $"{tarih.Year} Yerel Seçimleri" }).Entity;
        if (secim.Tur != SecimTuru.Yerel)
            throw new YuklemeHatasi($"{tarih:dd.MM.yyyy} tarihli seçim veritabanında {secim.Tur} olarak kayıtlı; yerel seçim dosyası yüklenemez.");
        return secim;
    }

    /// <summary>
    /// basliklar.csv'deki partileri ve başlık eşlemelerini veritabanına yazar (yoksa ekler, değiştiyse günceller).
    /// Dönen sözlük: kısa ad → veritabanındaki parti.
    /// </summary>
    private async Task<Dictionary<string, Parti>> PartileriEsitleAsync(CancellationToken ct)
    {
        var partiler = await db.Partiler.ToDictionaryAsync(p => p.KisaAd, ct);
        foreach (var p in tanimlar.Partiler.Where(p => !partiler.ContainsKey(p.KisaAd)))
            partiler[p.KisaAd] = db.Partiler.Add(new Parti { KisaAd = p.KisaAd }).Entity;

        var eslemeler = await db.PartiAdEslemeleri.ToListAsync(ct);
        foreach (var e in tanimlar.PartiEslemeleri)
        {
            var parti = partiler[e.Parti.KisaAd];
            var kayit = eslemeler.FirstOrDefault(x => x.HamAd == e.HamAd && x.GecerliBaslangic == e.GecerliBaslangic);
            if (kayit is null)
            {
                db.PartiAdEslemeleri.Add(new PartiAdEslemesi
                {
                    HamAd = e.HamAd, Parti = parti, GecerliBaslangic = e.GecerliBaslangic, GecerliBitis = e.GecerliBitis,
                });
            }
            else
            {
                kayit.Parti = parti;
                kayit.GecerliBitis = e.GecerliBitis;
            }
        }
        return partiler;
    }

    /// <summary>--yenile: eski dosyanın sonuçları ve oy özetleri silinir, kaydı "Yenilendi" olur.</summary>
    private async Task EskiYuklemeyiKaldirAsync(Yukleme aktif, CancellationToken ct)
    {
        await db.Sonuclar
            .Where(s => s.SecimId == aktif.SecimId && s.OyTuru == aktif.OyTuru && s.IlceId == aktif.IlceId)
            .ExecuteDeleteAsync(ct);
        await db.OyOzetleri
            .Where(s => s.SecimId == aktif.SecimId && s.OyTuru == aktif.OyTuru && s.IlceId == aktif.IlceId)
            .ExecuteDeleteAsync(ct);

        // Yeni "Aktif" kayıt eklenmeden önce kaydedilir; ikisi aynı anda aktif olamaz.
        aktif.Durum = YuklemeDurumu.Yenilendi;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>--yenile sonrası: hiçbir sonucu kalmayan sandıklar ve adaylar silinir. Öteki oy türlerinin kullandıkları kalır.</summary>
    private async Task YetimKayitlariSilAsync(Secim secim, Ilce ilce, OyTuru oyTuru, CancellationToken ct)
    {
        await db.Sandiklar
            .Where(s => s.SecimId == secim.Id && s.MahalleKoy.IlceId == ilce.Id
                        && !db.Sonuclar.Any(x => x.SandikId == s.Id) && !db.OyOzetleri.Any(x => x.SandikId == s.Id))
            .ExecuteDeleteAsync(ct);
        await db.Adaylar
            .Where(a => a.SecimId == secim.Id && a.OyTuru == oyTuru && !db.Sonuclar.Any(x => x.AdayId == a.Id))
            .ExecuteDeleteAsync(ct);
    }

    private async Task<Dictionary<int, MahalleKoy>> MahalleleriHazirlaAsync(MahalleOkumasi okuma, Ilce ilce, List<string> uyarilar, CancellationToken ct)
    {
        var kodlar = okuma.Adlar.Keys.ToList();
        var mevcut = await db.MahalleKoyler.Where(m => kodlar.Contains(m.Kod)).ToDictionaryAsync(m => m.Kod, ct);

        var yabanci = mevcut.Values.Where(m => m.IlceId != ilce.Id).Select(m => $"{m.Kod} '{m.Ad}'").ToList();
        if (yabanci.Count > 0)
            throw new YuklemeHatasi($"Şu Muhtarlık Id'ler veritabanında başka bir ilçeye kayıtlı: {string.Join(", ", yabanci)}.");

        foreach (var (kod, (ad, tur)) in okuma.Adlar)
        {
            if (mevcut.TryGetValue(kod, out var m))
            {
                if (m.Ad != ad)
                    uyarilar.Add($"Muhtarlık Id {kod} veritabanında '{m.Ad}', bu dosyada '{ad}' adıyla geçiyor; ad değiştirilmedi.");
                continue;
            }
            mevcut[kod] = db.MahalleKoyler.Add(new MahalleKoy { Kod = kod, Ad = ad, Tur = tur, Ilce = ilce }).Entity;
        }
        return mevcut;
    }

    /// <summary>Sandıklar seçime bağlıdır; aynı yılın öteki oy türü dosyaları aynı sandıkları kullanır.</summary>
    private async Task<Dictionary<(int MuhtarlikId, int SandikNo), Sandik>> SandiklariHazirlaAsync(
        YskSandikDosyasi dosya, Secim secim, Dictionary<int, MahalleKoy> mahalleler, CancellationToken ct)
    {
        var sonuc = new Dictionary<(int, int), Sandik>();
        if (secim.Id != 0)
        {
            var mahalleIdleri = mahalleler.Values.Where(m => m.Id != 0).Select(m => m.Id).ToList();
            var mevcut = await db.Sandiklar
                .Where(s => s.SecimId == secim.Id && mahalleIdleri.Contains(s.MahalleKoyId))
                .ToListAsync(ct);
            foreach (var s in mevcut)
                sonuc[(s.MahalleKoy.Kod, s.No)] = s;
        }

        foreach (var satir in dosya.Satirlar)
        {
            var anahtar = (satir.MuhtarlikId, satir.SandikNo);
            if (!sonuc.ContainsKey(anahtar))
                sonuc[anahtar] = db.Sandiklar.Add(new Sandik { No = satir.SandikNo, Secim = secim, MahalleKoy = mahalleler[satir.MuhtarlikId] }).Entity;
        }

        // Sözlükte yalnızca bu dosyanın sandıkları kalsın (öteki dosyaların fazladan sandıkları sayılmasın).
        return dosya.Satirlar.ToDictionary(s => (s.MuhtarlikId, s.SandikNo), s => sonuc[(s.MuhtarlikId, s.SandikNo)]);
    }

    /// <summary>Her eşlenmiş sütun için: oy dizisindeki sırası ve partisi ya da adayı.</summary>
    private async Task<List<(int Sira, Parti? Parti, Aday? Aday)>> SutunKalemleriAsync(
        EslesmeSonucu eslesme, Secim secim, OyTuru oyTuru, Il il, Ilce? adayIlcesi, Dictionary<string, Parti> partiler, CancellationToken ct)
    {
        int? ilceId = adayIlcesi?.Id; // boşsa EF "IlceId IS NULL" üretir
        var adaylar = secim.Id == 0
            ? []
            : await db.Adaylar
                .Where(a => a.SecimId == secim.Id && a.OyTuru == oyTuru && a.IlId == il.Id && a.IlceId == ilceId)
                .ToListAsync(ct);

        var liste = new List<(int, Parti?, Aday?)>();
        // Oylar dizisi dosyadaki sütun sırasındadır (4. sütun = 0. eleman).
        foreach (var e in eslesme.Eslesmeler)
        {
            var sira = e.SutunNo - 4;
            switch (e)
            {
                case PartiSutunu p:
                    liste.Add((sira, partiler[p.Parti.KisaAd], null));
                    break;
                case BagimsizSutunu b:
                    var aday = adaylar.FirstOrDefault(a => a.AdSoyad == b.AdSoyad);
                    if (aday is null)
                    {
                        aday = new Aday { AdSoyad = b.AdSoyad, Secim = secim, OyTuru = oyTuru, Il = il, Ilce = adayIlcesi };
                        db.Adaylar.Add(aday);
                        adaylar.Add(aday);
                    }
                    liste.Add((sira, null, aday));
                    break;
            }
        }
        return liste;
    }

    /// <summary>
    /// DSP sütunu varsa kendi listesi, yoksa girmedi. Büyükşehir başkanlığı il geneli, öteki oy türleri ilçe düzeyinde tutulur.
    /// Kayıt zaten varsa ve farklıysa üzerine yazılmaz (elle düzeltilmiş olabilir), uyarı verilir.
    /// </summary>
    private async Task DspKatiliminiYazAsync(Secim secim, DosyaKimligi kimlik, Il il, Ilce? ilce, long? dspOyu, List<string> uyarilar, CancellationToken ct)
    {
        var durum = dspOyu is null ? DspDurumu.Girmedi : DspDurumu.KendiListesi;
        var ittifak = durum == DspDurumu.Girmedi ? null : DspIttifaklari.GetValueOrDefault(kimlik.Yil);
        if (dspOyu == 0)
            uyarilar.Add("DSP sütunu var ama toplam oyu 0; durum 'KendiListesi' yazıldı, kontrol edin.");

        int? ilceId = ilce?.Id;
        var mevcut = secim.Id == 0 ? null : await db.DspKatilimlari.FirstOrDefaultAsync(d =>
            d.SecimId == secim.Id && d.OyTuru == kimlik.OyTuru && d.IlId == il.Id && d.IlceId == ilceId, ct);

        if (mevcut is null)
        {
            db.DspKatilimlari.Add(DspKatilimi.Olustur(secim, kimlik.OyTuru, il, ilce, durum, ittifakAdi: ittifak));
        }
        else if (mevcut.Durum != durum || mevcut.IttifakAdi != ittifak)
        {
            uyarilar.Add($"DSP katılımı veritabanında '{mevcut.Durum}{(mevcut.IttifakAdi is null ? "" : ", " + mevcut.IttifakAdi)}', " +
                         $"bu dosyaya göre '{durum}{(ittifak is null ? "" : ", " + ittifak)}'; kayıt değiştirilmedi.");
        }
    }

    /// <summary>Ham dosya içeriğe göre adlandırılarak saklanır; var olan arşiv dosyasının üzerine yazılmaz.</summary>
    private void Arsivle(byte[] icerik, string sha)
    {
        Directory.CreateDirectory(arsivKlasoru);
        var hedef = Path.Combine(arsivKlasoru, sha + ".xlsx");
        if (File.Exists(hedef))
        {
            var mevcutSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(hedef)));
            if (mevcutSha != sha)
                throw new YuklemeHatasi($"Arşivdeki {hedef} dosyası bozulmuş (SHA256 tutmuyor); elle kontrol edin.");
            return;
        }
        var gecici = hedef + ".yaziliyor";
        File.WriteAllBytes(gecici, icerik);
        File.Move(gecici, hedef);
    }
}
