using SecimSistemi.Importer;

Console.OutputEncoding = System.Text.Encoding.UTF8;

switch (args.FirstOrDefault())
{
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
        Console.WriteLine("Kullanım: incele <klasör> | karsilastir <a.xlsx> <b.xlsx>");
        return 1;
}
return 0;
