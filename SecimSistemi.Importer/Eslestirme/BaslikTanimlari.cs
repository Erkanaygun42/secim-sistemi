using System.Globalization;
using SecimSistemi.Domain;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer.Eslestirme;

/// <summary>Bir seçimde, bir oy türünde yarışan bağımsız aday. <see cref="AdSoyad"/> normalleştirilmiştir.</summary>
public sealed record BagimsizTanimi(string AdSoyad, int Yil, OyTuru OyTuru);

/// <summary>
/// <c>Tanimlar/basliklar.csv</c> dosyasının içeriği: parti başlıkları (<see cref="PartiAdEslemesi"/>) ve bağımsız adaylar.
/// Aynı ada sahip partiler tek bir <see cref="Parti"/> nesnesini paylaşır (örnek "İP" ve "VATAN PARTİSİ").
/// Dosyadaki hatalar satır numarasıyla birlikte, hepsi bir arada bildirilir.
/// </summary>
public sealed class BaslikTanimlari
{
    public const string BaslikSatiri = "ham_ad;tur;parti;baslangic;bitis;yil;oy_turu";

    public static string VarsayilanYol => Path.Combine(AppContext.BaseDirectory, "Tanimlar", "basliklar.csv");

    public IReadOnlyList<PartiAdEslemesi> PartiEslemeleri { get; }
    public IReadOnlyList<BagimsizTanimi> Bagimsizlar { get; }
    public IReadOnlyList<Parti> Partiler => PartiEslemeleri.Select(e => e.Parti).Distinct().ToList();

    private BaslikTanimlari(IReadOnlyList<PartiAdEslemesi> partiEslemeleri, IReadOnlyList<BagimsizTanimi> bagimsizlar)
    {
        PartiEslemeleri = partiEslemeleri;
        Bagimsizlar = bagimsizlar;
    }

    public static BaslikTanimlari Yukle(string? yol = null)
    {
        yol ??= VarsayilanYol;
        using var okuyucu = new StreamReader(yol, System.Text.Encoding.UTF8);
        return Oku(okuyucu, Path.GetFileName(yol));
    }

    public static BaslikTanimlari Oku(TextReader okuyucu, string kaynak)
    {
        var hatalar = new List<string>();
        var partiler = new Dictionary<string, Parti>();
        var eslemeler = new List<(int Satir, PartiAdEslemesi Esleme)>();
        var bagimsizlar = new List<(int Satir, BagimsizTanimi Tanim)>();
        bool baslikGoruldu = false;
        int satirNo = 0;

        for (var satir = okuyucu.ReadLine(); satir is not null; satir = okuyucu.ReadLine())
        {
            satirNo++;
            if (string.IsNullOrWhiteSpace(satir) || satir.TrimStart().StartsWith('#')) continue;

            if (!baslikGoruldu)
            {
                if (satir.Trim() != BaslikSatiri)
                    throw new YuklemeHatasi($"{kaynak}: {satirNo}. satır '{BaslikSatiri}' olmalı.");
                baslikGoruldu = true;
                continue;
            }

            var alan = satir.Split(';').Select(a => a.Trim()).ToArray();
            if (alan.Length != 7)
            {
                hatalar.Add($"{satirNo}. satır: 7 alan bekleniyordu, {alan.Length} var");
                continue;
            }
            var (hamAd, tur, partiAdi, baslangic, bitis, yil, oyTuru) = (alan[0], alan[1], alan[2], alan[3], alan[4], alan[5], alan[6]);
            if (hamAd.Length == 0)
            {
                hatalar.Add($"{satirNo}. satır: ham_ad boş");
                continue;
            }

            switch (tur)
            {
                case "parti":
                    if (partiAdi.Length == 0) hatalar.Add($"{satirNo}. satır: partide 'parti' alanı dolu olmalı");
                    if (yil.Length > 0 || oyTuru.Length > 0) hatalar.Add($"{satirNo}. satır: partide 'yil' ve 'oy_turu' boş olmalı");
                    var bas = Tarih(baslangic, satirNo, "baslangic", hatalar);
                    var bit = Tarih(bitis, satirNo, "bitis", hatalar);
                    if (bas > bit) hatalar.Add($"{satirNo}. satır: baslangic bitis'ten sonra");
                    if (partiAdi.Length == 0) break;

                    var kisaAd = PartiAdEslemesi.Normallestir(partiAdi);
                    if (!partiler.TryGetValue(kisaAd, out var parti))
                        partiler[kisaAd] = parti = new Parti { KisaAd = kisaAd };
                    eslemeler.Add((satirNo, new PartiAdEslemesi
                    {
                        HamAd = PartiAdEslemesi.Normallestir(hamAd),
                        Parti = parti,
                        GecerliBaslangic = bas,
                        GecerliBitis = bit,
                    }));
                    break;

                case "bagimsiz":
                    if (partiAdi.Length > 0 || baslangic.Length > 0 || bitis.Length > 0)
                        hatalar.Add($"{satirNo}. satır: bağımsızda 'parti', 'baslangic' ve 'bitis' boş olmalı");
                    if (!int.TryParse(yil, NumberStyles.None, CultureInfo.InvariantCulture, out var y))
                        hatalar.Add($"{satirNo}. satır: bağımsızda 'yil' dolu olmalı (örnek 2024)");
                    if (!OyTuruAdlari.TryCoz(oyTuru, out var ot))
                        hatalar.Add($"{satirNo}. satır: '{oyTuru}' tanımlı bir oy türü değil ({string.Join(", ", OyTuruAdlari.Hepsi)})");
                    else if (y > 0)
                        bagimsizlar.Add((satirNo, new BagimsizTanimi(PartiAdEslemesi.Normallestir(hamAd), y, ot)));
                    break;

                default:
                    hatalar.Add($"{satirNo}. satır: tur '{tur}' olamaz; 'parti' ya da 'bagimsiz' yazılmalı");
                    break;
            }
        }

        if (!baslikGoruldu)
            throw new YuklemeHatasi($"{kaynak}: başlık satırı yok.");

        // Aynı başlığın iki eşlemesi aynı tarihte geçerli olamaz; yoksa hangi partiye gideceği belirsiz kalır.
        foreach (var grup in eslemeler.GroupBy(e => e.Esleme.HamAd))
        {
            var liste = grup.ToList();
            for (int i = 0; i < liste.Count; i++)
            for (int j = i + 1; j < liste.Count; j++)
            {
                if (Cakisiyor(liste[i].Esleme, liste[j].Esleme))
                    hatalar.Add($"{liste[j].Satir}. satır: '{grup.Key}' için {liste[i].Satir}. satırla tarih aralığı çakışıyor");
            }
        }

        var partiBasliklari = eslemeler.Select(e => e.Esleme.HamAd).ToHashSet();
        foreach (var grup in bagimsizlar.GroupBy(b => b.Tanim))
        {
            var ilk = grup.First();
            foreach (var tekrar in grup.Skip(1))
                hatalar.Add($"{tekrar.Satir}. satır: {ilk.Satir}. satırın tekrarı");
            if (partiBasliklari.Contains(ilk.Tanim.AdSoyad))
                hatalar.Add($"{ilk.Satir}. satır: '{ilk.Tanim.AdSoyad}' hem parti hem bağımsız olarak tanımlı");
        }

        if (hatalar.Count > 0)
            throw new YuklemeHatasi($"{kaynak}: {hatalar.Count} hata var:{Environment.NewLine}{string.Join(Environment.NewLine, hatalar.Select(h => "  " + h))}");

        return new BaslikTanimlari(eslemeler.Select(e => e.Esleme).ToList(), bagimsizlar.Select(b => b.Tanim).ToList());
    }

    private static DateOnly? Tarih(string deger, int satirNo, string alanAdi, List<string> hatalar)
    {
        if (deger.Length == 0) return null;
        if (DateOnly.TryParseExact(deger, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t;
        hatalar.Add($"{satirNo}. satır: {alanAdi} '{deger}' yyyy-aa-gg biçiminde değil");
        return null;
    }

    private static bool Cakisiyor(PartiAdEslemesi a, PartiAdEslemesi b) =>
        (a.GecerliBaslangic ?? DateOnly.MinValue) <= (b.GecerliBitis ?? DateOnly.MaxValue) &&
        (b.GecerliBaslangic ?? DateOnly.MinValue) <= (a.GecerliBitis ?? DateOnly.MaxValue);
}
