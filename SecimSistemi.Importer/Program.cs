using SecimSistemi.Importer;

Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    switch (args.FirstOrDefault())
    {
        case "basliklari-denetle" when args.Length == 2:
            return BaslikDenetimi.Calistir(args[1]);
        case "incele" when args.Length == 2:
            Incele.Klasor(args[1]);
            break;
        case "ozet" when args.Length == 2:
            Incele.Ozet(args[1]);
            break;
        case "karsilastir" when args.Length == 3:
            Incele.Karsilastir(args[1], args[2]);
            break;
        default:
            Console.WriteLine("Kullanım:");
            Console.WriteLine("  basliklari-denetle <klasör>   Dosya başlıklarını Tanimlar/basliklar.csv ile eşler (veritabanına dokunmaz)");
            Console.WriteLine("  incele <klasör> | ozet <klasör> | karsilastir <a.xlsx> <b.xlsx>");
            return 1;
    }
    return 0;
}
catch (YuklemeHatasi hata)
{
    Console.Error.WriteLine(hata.Message);
    return 1;
}
