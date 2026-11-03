namespace SecimSistemi.Domain;

/// <summary>Bir seçim günü. Aynı gün yapılan oy türleri (meclis, başkan, …) aynı seçime aittir.</summary>
public class Secim
{
    public int Id { get; set; }
    public required DateOnly Tarih { get; set; }
    public required SecimTuru Tur { get; set; }
    public required string Ad { get; set; }
}
