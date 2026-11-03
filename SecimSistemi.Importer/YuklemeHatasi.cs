namespace SecimSistemi.Importer;

/// <summary>
/// Kullanıcının düzeltmesi gereken bir sorun: hatalı dosya adı, tanımsız başlık, bozuk hücre gibi.
/// Program bu hatayı yığın izi olmadan, yalnızca mesajıyla gösterir.
/// </summary>
public class YuklemeHatasi(string mesaj) : Exception(mesaj);
