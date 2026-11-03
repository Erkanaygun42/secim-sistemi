using SecimSistemi.Importer;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Mahalleler;
using SecimSistemi.Importer.Rapor;

Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    // Seçenekler: --yenile (değersiz), --oy-turu <değer>. Geri kalanlar konumsal.
    bool yenile = false;
    string? oyTuru = null;
    var liste = new List<string>();
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--yenile":
                yenile = true;
                break;
            case "--oy-turu":
                oyTuru = i + 1 < args.Length ? args[++i] : throw new YuklemeHatasi("--oy-turu bir değer ister (örnek belediye-meclisi).");
                break;
            case var s when s.StartsWith("--", StringComparison.Ordinal):
                throw new YuklemeHatasi($"Bilinmeyen seçenek: {s}");
            default:
                liste.Add(args[i]);
                break;
        }
    }
    var konumsal = liste.ToArray();

    switch (konumsal.FirstOrDefault())
    {
        case "rapor" when konumsal.Length == 3 && konumsal[1] == "erime":
            return await RaporKomutlari.ErimeAsync(konumsal[2], oyTuru);
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
            Console.WriteLine("  rapor erime <ilçe kodu> [--oy-turu belediye-meclisi|belediye-baskani|buyuksehir-baskani]");
            Console.WriteLine("                                      2009 tabanlı erime raporu; csv'ler data/out altına yazılır");
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
