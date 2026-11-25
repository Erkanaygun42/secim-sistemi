# Yeni ilçe ekleme

Bu belge, sisteme yeni bir ilçenin (örnek: Şişli) nasıl ekleneceğini adım adım anlatır. Komutlar depo klasöründe
(`C:\dev\secim-sistemi`) PowerShell'de çalıştırılır. Veritabanı silinmez; yükleme yalnızca ekler (aynı dosya iki kez yüklenmez).

Şu an tanımlı ilçeler: Esenyurt, Şişli, Adalar, Bakırköy (İstanbul). Başka bir ilçe için önce [0. adım](#0-ilce-tanımlı-değilse).

## 1. Dosyaları indir ve koy

YSK Açık Veri Portalı (https://acikveri.ysk.gov.tr), her ilçe için 4 seçim × 3 oy türü = 12 dosya:

1. Açılan tanıtım penceresini sağ üstteki × ile kapatın.
2. Üstte **Seçim Seçiniz** → oy türünün grubu (**BELEDİYE MECLİSİ ÜYELİĞİ SEÇİMLERİ**, **BELEDİYE BAŞKANLIĞI SEÇİMLERİ**,
   **BÜYÜKŞEHİR BELEDİYE BAŞKANLIĞI SEÇİMLERİ**) → yıl (31 Mart 2024, 31 Mart 2019, 30 Mart 2014, 29 Mart 2009).
3. Sol menüde **SEÇİM SONUÇ VERİLERİ** altındaki sonuç sayfası (örnek "Belediye Meclisi Seçim Sonuçları") → il ve ilçeyi seçin →
   **Excel İndir**.
4. Dosyayı şu adla `data/raw` klasörüne kaydedin:

   `{yıl}_yerel_{oy-türü}_{ilçe}.xlsx` — oy türü: `belediye-meclisi`, `belediye-baskani`, `buyuksehir-baskani`;
   ilçe: küçük harf, Türkçe harfsiz (`sisli`, `adalar`, `bakirkoy`). Örnek: `2024_yerel_belediye-meclisi_sisli.xlsx`.

`data/` klasörü git'e girmez.

## 2. Denetimi çalıştır

```powershell
dotnet run --project SecimSistemi.Importer -- dosya-denetle data/raw
```

Çıktıda her ilçe için 4 yıl × 3 oy türü işaretlenir (✓ var, · eksik). Denetim şunları **hata** sayar ve yüklemeyi durdurur:
xlsx olmayan dosya, düzene uymayan ad (doğru ad tahmin edilebiliyorsa yazılır), açılamayan dosya, içeriği aynı iki dosya
(aynı ilçe iki kez indirilmiş), aynı yılda başka bir ilçenin mahallelerini içeren dosya (ilçe adı yanlış). Eksik dosyalar ve
farklı sandık sayıları yalnızca **uyarıdır**. Hata kalmayana kadar dosyaları düzeltip denetimi yeniden çalıştırın.

## 3. Yükle

```powershell
dotnet run --project SecimSistemi.Importer -- yukle-hepsi data/raw
```

`yukle-hepsi` önce denetimi kendisi çalıştırır; hata varsa hiçbir dosya yüklenmez. Daha önce yüklenmiş dosyalar atlanır.
Her dosyadan sonra yüklenen satır sayısı ve süre yazılır.

## 4. Bilinmeyen parti başlıklarını sınıflandır

Dosyalarda tanımlı olmayan bir parti ya da aday başlığı varsa yükleme durur ve başlığı yazar. Yüklemeden önce bütün
dosyalardaki bilinmeyen başlıkları tek listede görmek için:

```powershell
dotnet run --project SecimSistemi.Importer -- basliklari-denetle data/raw
```

Her başlık için `SecimSistemi.Importer/Tanimlar/basliklar.csv` dosyasına bir satır ekleyin: `tur` alanına parti ya da
bağımsız aday için `parti` / `bagimsiz` yazın (biçim dosyanın başındaki açıklamada). Başlık tahminle sınıflanmaz. Sonra
3. adımı yeniden çalıştırın; yüklenmiş dosyalar atlanır, kalanlar yüklenir.

## 5. Harita sınırlarını ekle

```powershell
dotnet run --project SecimSistemi.Importer -- harita-sinirlari sisli
```

Komut ilçenin mahalle sınırlarını OpenStreetMap'ten (Overpass API) bir kez indirir ve `SecimSistemi.Web/Harita/sisli.geojson`
olarak kaydeder (internet gerekir; harita sonra internetsiz çalışır). Overpass sunucusu doluysa komut bekleyip yeniden dener;
yine olmazsa birkaç dakika sonra tekrar çalıştırın. Dosya git'e girer; ODbL atfı dosyanın içinde ve haritanın altında yazılıdır.

Uygulamayı yeniden başlatın. Harita sayfasında (`/harita`) eşleşmeyen mahalleler listelenir. OSM adı YSK adıyla eşleşmiyorsa
`SecimSistemi.Importer/Tanimlar/harita-eslemeleri.csv` dosyasına `ilce;osm_id;birim_kod;not` satırı ekleyin (osm_id listede
yazılı; birim_kod YSK mahalle kodu, karne sayfasındaki "Kod" sütunu).

## 6. Kontrol et

```powershell
dotnet run --project SecimSistemi.Web
```

http://localhost:5058 → **İl özeti**'nde yeni ilçe kendiliğinden görünür; karne, erime, gruplar, öncelik, meclis ve harita
sayfalarında ilçe seçicisinde listelenir. Doğrulanmamış değerler (meclis üye sayısı, sınır şüphesi, eşlenmemiş mahalleler)
sayfalarda uyarıyla işaretlidir.

Meclis hesaplayıcısı için ilçenin meclis üye sayısı `SecimSistemi.Importer/Tanimlar/meclis-uye-sayilari.csv` dosyasında yoksa
sayfa 45 üyeyle hesaplar ve "doğrulanmadı" uyarısı gösterir. Resmî sayılar YSK'nın "Seçilen Belediye Meclisi Üyeleri Listesi"
veri setinden gelecek (okuyucusu dosya gelince yazılacak).

## 0. İlçe tanımlı değilse

`SecimSistemi.Importer/Okuma/DosyaKimligi.cs` içinde `BolgeTanimlari.Ilceler` listesine bir satır ekleyin:

```csharp
["kadikoy"] = new("kadikoy", "34-KADIKÖY", "KADIKÖY", Istanbul),
```

İlk değer dosya adındaki yazım, ikincisi ilçe kodu (plaka-AD), üçüncüsü ilçe adı (YSK'daki gibi büyük harf). Başka bir il
için aynı dosyada bir `IlTanimi` ekleyin (örnek `new(41, "KOCAELİ")`). Değişiklikten sonra `dotnet build` ve 2. adım.
