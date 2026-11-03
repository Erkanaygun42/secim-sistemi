using SecimSistemi.Domain;
using SecimSistemi.Importer.Kontrol;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Eslestirme;

public abstract record SutunEslesmesi(int SutunNo, string HamBaslik)
{
    public abstract Kalem Kalem { get; }
}

public sealed record PartiSutunu(int SutunNo, string HamBaslik, Parti Parti) : SutunEslesmesi(SutunNo, HamBaslik)
{
    public override Kalem Kalem => Kalem.Parti(Parti.KisaAd);
}

public sealed record BagimsizSutunu(int SutunNo, string HamBaslik, string AdSoyad) : SutunEslesmesi(SutunNo, HamBaslik)
{
    public override Kalem Kalem => Kalem.Aday(AdSoyad);
}

public sealed record EslesmeSonucu(
    IReadOnlyList<SutunEslesmesi> Eslesmeler,
    IReadOnlyList<Baslik> Bilinmeyenler,
    IReadOnlyList<string> Hatalar)
{
    public bool BasariliMi => Bilinmeyenler.Count == 0 && Hatalar.Count == 0;

    /// <summary>
    /// Bilinmeyen başlık ya da çift eşleme varsa hepsini listeleyen bir <see cref="BilinmeyenBaslikHatasi"/> fırlatır.
    /// Hiçbir başlık atlanmaz ya da tahminle sınıflanmaz.
    /// </summary>
    public void BasarisizsaDur(string dosyaAdi, DateOnly secimTarihi, OyTuru oyTuru)
    {
        if (!BasariliMi) throw new BilinmeyenBaslikHatasi(dosyaAdi, secimTarihi, oyTuru, this);
    }
}

public sealed class BilinmeyenBaslikHatasi(string dosyaAdi, DateOnly secimTarihi, OyTuru oyTuru, EslesmeSonucu sonuc)
    : YuklemeHatasi(Mesaj(dosyaAdi, secimTarihi, oyTuru, sonuc))
{
    public EslesmeSonucu Sonuc { get; } = sonuc;

    private static string Mesaj(string dosyaAdi, DateOnly secimTarihi, OyTuru oyTuru, EslesmeSonucu s)
    {
        var n = Environment.NewLine;
        var parcalar = new List<string> { $"{dosyaAdi}: başlıklar eşlenemedi, yükleme durduruldu." };
        if (s.Bilinmeyenler.Count > 0)
        {
            parcalar.Add($"Tanımsız {s.Bilinmeyenler.Count} başlık:");
            parcalar.AddRange(s.Bilinmeyenler.Select(b => $"  {b.SutunNo}. sütun: '{b.Ham}'"));
            parcalar.Add("Tanimlar/basliklar.csv dosyasına ekleyin. 'tur' alanına parti ya da bagimsiz yazın;");
            parcalar.Add("partide 'parti' alanını doldurup 'yil' ve 'oy_turu' alanlarını silin:");
            var oyTuruAdi = OyTuruAdlari.Ad(oyTuru);
            parcalar.AddRange(s.Bilinmeyenler.Select(b => $"  {PartiAdEslemesi.Normallestir(b.Ham)};;;;;{secimTarihi.Year};{oyTuruAdi}"));
        }
        if (s.Hatalar.Count > 0)
        {
            parcalar.Add("Eşleme hataları:");
            parcalar.AddRange(s.Hatalar.Select(h => "  " + h));
        }
        return string.Join(n, parcalar);
    }
}

/// <summary>
/// Dosyadaki parti/aday sütun başlıklarını <see cref="BaslikTanimlari"/> ile eşler. Önce parti eşlemesi
/// (seçim tarihinde geçerli olan), sonra o seçim ve oy türündeki bağımsız aday listesi aranır.
/// </summary>
public sealed class BaslikEslestirici(BaslikTanimlari tanimlar)
{
    public EslesmeSonucu Eslestir(IReadOnlyList<Baslik> basliklar, DateOnly secimTarihi, OyTuru oyTuru)
    {
        var eslesmeler = new List<SutunEslesmesi>();
        var bilinmeyenler = new List<Baslik>();

        foreach (var baslik in basliklar)
        {
            var ad = PartiAdEslemesi.Normallestir(baslik.Ham);

            // Tanımlar yüklenirken aynı başlığın çakışan eşlemeleri reddedildiği için en fazla bir eşleme bulunur.
            var esleme = tanimlar.PartiEslemeleri.SingleOrDefault(e => e.HamAd == ad && e.GecerliMi(secimTarihi));
            if (esleme is not null)
            {
                eslesmeler.Add(new PartiSutunu(baslik.SutunNo, baslik.Ham, esleme.Parti));
                continue;
            }

            if (ad.Length > 0 && tanimlar.Bagimsizlar.Any(b => b.AdSoyad == ad && b.Yil == secimTarihi.Year && b.OyTuru == oyTuru))
            {
                eslesmeler.Add(new BagimsizSutunu(baslik.SutunNo, baslik.Ham, ad));
                continue;
            }

            bilinmeyenler.Add(baslik);
        }

        // İki sütun aynı partiye düşerse (örnek aynı dosyada hem "İP" hem "VATAN PARTİSİ") oylar karışır.
        var hatalar = eslesmeler
            .GroupBy(e => e.Kalem)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key.Ad}' birden çok sütuna eşlendi: {string.Join(", ", g.Select(e => $"{e.SutunNo}. sütun '{e.HamBaslik}'"))}")
            .ToList();

        return new EslesmeSonucu(eslesmeler, bilinmeyenler, hatalar);
    }
}
