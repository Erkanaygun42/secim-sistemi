namespace SecimSistemi.Domain;

/// <summary>
/// Bir sonucun ya da oy özetinin ait olduğu bölge. Seçilen düzeyin bütün üst bölgeleri de doldurulur:
/// bir sandık bölgesi aynı zamanda mahallesini, ilçesini ve ilini taşır.
/// Böylece farklı düzeyde gelen veriler (1999 ilçe, 2024 sandık) ortak üst düzeyde doğrudan gruplanabilir.
/// </summary>
public sealed record Bolge
{
    public BolgeDuzeyi Duzey { get; }
    public Il Il { get; }
    public Ilce? Ilce { get; }
    public MahalleKoy? MahalleKoy { get; }
    public Sandik? Sandik { get; }

    private Bolge(BolgeDuzeyi duzey, Il il, Ilce? ilce, MahalleKoy? mahalleKoy, Sandik? sandik)
    {
        Duzey = duzey;
        Il = il;
        Ilce = ilce;
        MahalleKoy = mahalleKoy;
        Sandik = sandik;
    }

    public static Bolge IlDuzeyinde(Il il)
    {
        ArgumentNullException.ThrowIfNull(il);
        return new Bolge(BolgeDuzeyi.Il, il, null, null, null);
    }

    public static Bolge IlceDuzeyinde(Ilce ilce)
    {
        ArgumentNullException.ThrowIfNull(ilce);
        var il = ilce.Il ?? throw new ArgumentException($"'{ilce.Ad}' ilçesinin ili yüklenmemiş.", nameof(ilce));
        return new Bolge(BolgeDuzeyi.Ilce, il, ilce, null, null);
    }

    public static Bolge MahalleKoyDuzeyinde(MahalleKoy mahalleKoy)
    {
        ArgumentNullException.ThrowIfNull(mahalleKoy);
        var ilce = mahalleKoy.Ilce ?? throw new ArgumentException($"'{mahalleKoy.Ad}' biriminin ilçesi yüklenmemiş.", nameof(mahalleKoy));
        var ust = IlceDuzeyinde(ilce);
        return new Bolge(BolgeDuzeyi.MahalleKoy, ust.Il, ilce, mahalleKoy, null);
    }

    public static Bolge SandikDuzeyinde(Sandik sandik)
    {
        ArgumentNullException.ThrowIfNull(sandik);
        var mahalleKoy = sandik.MahalleKoy ?? throw new ArgumentException($"{sandik.No} numaralı sandığın mahalle/köyü yüklenmemiş.", nameof(sandik));
        var ust = MahalleKoyDuzeyinde(mahalleKoy);
        return new Bolge(BolgeDuzeyi.Sandik, ust.Il, ust.Ilce, mahalleKoy, sandik);
    }
}
