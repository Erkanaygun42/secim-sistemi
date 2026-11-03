using SecimSistemi.Importer;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Mahalleler;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var yenile = args.Contains("--yenile");
var konumsal = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var bilinmeyenSecenek = args.FirstOrDefault(a => a.StartsWith("--", StringComparison.Ordinal) && a != "--yenile");

try
{
    if (bilinmeyenSecenek is not null)
        throw new YuklemeHatasi($"Bilinmeyen seçenek: {bilinmeyenSecenek}");

    switch (konumsal.FirstOrDefault())
    {
        case "yukle" when konumsal.Length == 2:
            return await AktarimKomutlari.YukleAsync([konumsal[1]], yenile);
        case "yukle-hepsi" when konumsal.Length == 2:
            return await AktarimKomutlari.YukleHepsiAsync(konumsal[1], yenile);
        case "mahalle" when konumsal.Length == 3 && konumsal[1] == "listele":
            return await MahalleKomutlari.ListeleAsync(konumsal[2]);
        case "mahalle" when konumsal.Length == 3 && konumsal[1] == "esleme-oner":
            return await MahalleKomutlari.EslemeOnerAsync(konumsal[2]);
        case "mahalle" when konumsal.Length == 3 && konumsal[1] == "esleme-yukle":
            return await MahalleKomutlari.EslemeYukleAsync(konumsal[2]);
        case "basliklari-denetle" when konumsal.Length == 2:
            return BaslikDenetimi.Calistir(konumsal[1]);
        case "incele" when konumsal.Length == 2:
            Incele.Klasor(konumsal[1]);
            break;
        case "ozet" when konumsal.Length == 2:
            Incele.Ozet(konumsal[1]);
            break;
        case "karsilastir" when konumsal.Length == 3:
            Incele.Karsilastir(konumsal[1], konumsal[2]);
            break;
        default:
            Console.WriteLine("Kullanım:");
            Console.WriteLine("  yukle <dosya.xlsx> [--yenile]       Bir sandık dosyasını yükler");
            Console.WriteLine("  yukle-hepsi <klasör> [--yenile]     Klasördeki bütün sandık dosyalarını yükler (kontrol dosyaları hariç)");
            Console.WriteLine("  mahalle listele <ilçe kodu>         Mahallelerin hangi seçimlerde göründüğü ve eşleme ipuçları");
            Console.WriteLine("  mahalle esleme-oner <ilçe kodu>     Ad benzerliğine göre öneriler Tanimlar/mahalle-eslemeleri.csv'ye eklenir (onaylı sayılmaz)");
            Console.WriteLine("  mahalle esleme-yukle <ilçe kodu>    Onaylı eşlemeleri yazar, karşılaştırma birimlerini yeniden kurar");
            Console.WriteLine("  basliklari-denetle <klasör>         Başlıkları Tanimlar/basliklar.csv ile eşler (veritabanına dokunmaz)");
            Console.WriteLine("  incele <klasör> | ozet <klasör> | karsilastir <a.xlsx> <b.xlsx>");
            Console.WriteLine("--yenile: aynı seçim + oy türü + ilçe başka bir dosyayla yüklenmişse onun yerine geçer.");
            return 1;
    }
    return 0;
}
catch (YuklemeHatasi hata)
{
    Console.Error.WriteLine(hata.Message);
    return 1;
}
