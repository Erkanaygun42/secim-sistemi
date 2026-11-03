using SecimSistemi.Importer.Eslestirme;
using SecimSistemi.Importer.Okuma;

namespace SecimSistemi.Importer;

/// <summary>
/// Klasördeki bütün sandık dosyalarının başlıklarını <c>Tanimlar/basliklar.csv</c> ile eşler ve sonucu yazdırır.
/// Veritabanına dokunmaz; tanım dosyasını düzenledikten sonra kontrol için kullanılır.
/// </summary>
internal static class BaslikDenetimi
{
    public static int Calistir(string klasor)
    {
        var tanimlar = BaslikTanimlari.Yukle();
        var eslestirici = new BaslikEslestirici(tanimlar);
        Console.WriteLine($"Tanımlar: {tanimlar.PartiEslemeleri.Count} parti başlığı ({tanimlar.Partiler.Count} parti), {tanimlar.Bagimsizlar.Count} bağımsız aday satırı.");

        int sorunlu = 0;
        foreach (var yol in Directory.GetFiles(klasor, "*.xlsx").Order())
        {
            var kimlik = DosyaKimligi.Coz(yol);
            if (kimlik.KontrolDosyasiMi)
            {
                Console.WriteLine($"{kimlik.DosyaAdi}: kontrol dosyası, atlandı.");
                continue;
            }

            var dosya = YskDosyaOkuyucu.Oku(yol);
            var sonuc = eslestirici.Eslestir(dosya.OySutunlari, kimlik.SecimTarihi, kimlik.OyTuru);
            var parti = sonuc.Eslesmeler.OfType<PartiSutunu>().Count();
            var aday = sonuc.Eslesmeler.OfType<BagimsizSutunu>().Count();
            Console.WriteLine($"{kimlik.DosyaAdi}: {dosya.OySutunlari.Count} sütun = {parti} parti + {aday} bağımsız{(sonuc.BasariliMi ? "" : $", {sonuc.Bilinmeyenler.Count} tanımsız")}");

            try
            {
                sonuc.BasarisizsaDur(kimlik.DosyaAdi, kimlik.SecimTarihi, kimlik.OyTuru);
            }
            catch (BilinmeyenBaslikHatasi hata)
            {
                sorunlu++;
                Console.WriteLine(hata.Message);
            }
        }

        Console.WriteLine(sorunlu == 0 ? "SONUÇ: Bütün başlıklar tanımlı." : $"SONUÇ: {sorunlu} dosyada sorun var.");
        return sorunlu == 0 ? 0 : 1;
    }
}
