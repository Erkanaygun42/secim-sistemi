using SecimSistemi.Importer;
using SecimSistemi.Importer.Aktarim;
using SecimSistemi.Importer.Mahalleler;
using SecimSistemi.Importer.Rapor;

Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    var s = new Secenekler(args);
    var k = s.Konumsal;

    switch (k.FirstOrDefault())
    {
        case "rapor" when k.Count == 3 && k[1] == "erime":
            return await RaporKomutlari.ErimeAsync(k[2], s.Deger("--oy-turu"), s.Esikler());
        case "rapor" when k.Count == 3 && k[1] == "esik-dogrulama":
            return await RaporKomutlari.EsikDogrulamaAsync(k[2], s.Esikler());
        case "karar-talep" when k.Count == 2:
            return await KararKomutlari.TalepAsync(k[1], s);
        case "karar-onayla" when k.Count == 2:
            return await KararKomutlari.OnaylaAsync(k[1], s);
        case "karar-listele" when k.Count == 2:
            return await KararKomutlari.ListeleAsync(k[1], s.Bayrak("--hepsi"));
        case "yukle" when k.Count == 2:
            return await AktarimKomutlari.YukleAsync([k[1]], s.Bayrak("--yenile"));
        case "yukle-hepsi" when k.Count == 2:
            return await AktarimKomutlari.YukleHepsiAsync(k[1], s.Bayrak("--yenile"));
        case "mahalle" when k.Count == 3 && k[1] == "listele":
            return await MahalleKomutlari.ListeleAsync(k[2], s.Esikler());
        case "mahalle" when k.Count == 3 && k[1] == "esleme-oner":
            return await MahalleKomutlari.EslemeOnerAsync(k[2]);
        case "mahalle" when k.Count == 3 && k[1] == "esleme-yukle":
            return await MahalleKomutlari.EslemeYukleAsync(k[2], s.Zorunlu("--kisi", "onaylı csv satırlarını karar tablosuna aktaran kişi"));
        case "basliklari-denetle" when k.Count == 2:
            return BaslikDenetimi.Calistir(k[1]);
        case "incele" when k.Count == 2:
            Incele.Klasor(k[1]);
            break;
        case "ozet" when k.Count == 2:
            Incele.Ozet(k[1]);
            break;
        case "karsilastir" when k.Count == 3:
            Incele.Karsilastir(k[1], k[2]);
            break;
        default:
            Console.WriteLine("""
                Kullanım:
                  yukle <dosya.xlsx> [--yenile]           Bir sandık dosyasını yükler
                  yukle-hepsi <klasör> [--yenile]         Klasördeki bütün sandık dosyalarını yükler (kontrol dosyaları hariç)
                  mahalle listele <ilçe kodu>             Mahallelerin seçimlerdeki varlığı ve sınır durumu
                  mahalle esleme-oner <ilçe kodu>         Ad benzerliği önerileri Tanimlar/mahalle-eslemeleri.csv'ye eklenir (onaylı sayılmaz)
                  mahalle esleme-yukle <ilçe kodu> --kisi <ad>
                                                          csv'deki onaylı satırları karar tablosuna aktarır, birimleri yeniden kurar
                  karar-talep <ilçe kodu> --mahalle <kod> --gerekce "..." --kisi <ad> [--kaynak "..."]
                              (--secim <yıl> --karar sabit|siniri-degisti) | (--esle <yeni kod> --tur bolunme|birlesme|ad-degisikligi|kod-degisikligi [--yururluk yyyy-aa-gg])
                  karar-onayla <karar no> --kisi <ad> [--reddet] [--not "..."]
                  karar-listele <ilçe kodu> [--hepsi]     Talepler ve geçerli kararlar (--hepsi: geçmiş dahil)
                  rapor erime <ilçe kodu> [--oy-turu belediye-meclisi|belediye-baskani|buyuksehir-baskani]
                                                          Erime raporu; csv'ler data/out altına yazılır
                  rapor esik-dogrulama <ilçe kodu>        Onaylı insan kararlarıyla otomatik sınır tespitinin örtüşmesi
                  basliklari-denetle <klasör>             Başlıkları Tanimlar/basliklar.csv ile eşler (veritabanına dokunmaz)
                  incele <klasör> | ozet <klasör> | karsilastir <a.xlsx> <b.xlsx>
                Sınır eşikleri (mahalle listele, rapor erime, rapor esik-dogrulama):
                  --dusus-esigi 0.25  --artis-esigi 0.60  --supheli-esigi 0.15   (pay değişimi, oran olarak)
                --yenile: aynı seçim + oy türü + ilçe başka bir dosyayla yüklenmişse onun yerine geçer.
                """);
            return 1;
    }
    return 0;
}
catch (YuklemeHatasi hata)
{
    Console.Error.WriteLine(hata.Message);
    return 1;
}
