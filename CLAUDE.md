# Seçim Sistemi (DSP) — Proje notları

## Proje
- Demokratik Sol Parti için seçim sonuçlarını il, ilçe, mahalle/köy ve sandık düzeyinde analiz eden web uygulaması.
- Amaç: partinin hangi bölgelerde ne kadar eridiğini ve nerede potansiyeli olduğunu bulmak.
- Çıktılar: mahalle karnesi, erime raporu, öncelik listesi, harita, potansiyel skoru; her rapor PDF ve Excel olarak alınabilir.
- 2027 sonunda seçim günü modülü eklenecek (çevrimdışı müşahit uygulaması, YSK verisiyle karşılaştırma).
- Geliştirici C# biliyor ama .NET iş uygulamalarına yeni. Açıklamaları Türkçe yap, adımları küçük tut, büyük değişiklikten önce plan sun.

## Teknoloji
- .NET 10 (LTS), C#, hedef çerçeve `net10.0`
- ASP.NET Core Blazor Web App (panolar), seçim günü için Minimal API
- Entity Framework Core + Npgsql, PostgreSQL + PostGIS
- ClosedXML (Excel okuma), MudBlazor (arayüz), Leaflet (harita), Playwright (PDF), xUnit (test)
- Geliştirme ortamı: Windows, VS Code, PowerShell

## Çözüm yapısı (`SecimSistemi.sln`)
| Proje | Tür | Görev |
| --- | --- | --- |
| SecimSistemi.Domain | classlib | Varlıklar: Secim, OyTuru, Il, Ilce, MahalleKoy, Sandik, Parti, Sonuc, DspKatilimi |
| SecimSistemi.Data | classlib | EF Core DbContext, migration'lar |
| SecimSistemi.Importer | console | YSK Excel dosyalarını okur, doğrular, yükler |
| SecimSistemi.Tests | xunit | Toplam kontrolleri ve hesaplama kurallarının testleri |
| SecimSistemi.Web | blazor | Panolar, raporlar, PDF/Excel çıktısı, kullanıcı girişi |

Referanslar: Data → Domain; Importer → Data; Web → Data; Tests → Domain, Data, Importer ve Web.

## Veri modeli kuralları
- Sonuçlar "uzun" biçimde tutulur: bölge, seçim, oy türü, parti ya da aday, oy sayısı. Yeni parti veya seçim tablo yapısını değiştirmez.
- Sonuç hangi düzeyde geldiyse orada saklanır: 2009 sonrası sandık, 1991–2007 ilçe düzeyinde. Karşılaştırmalar iki seçimin ortak olduğu en ince düzeyde yapılır.
- Bölge hiyerarşisi: Il › Ilce › MahalleKoy › Sandik. Il, Ilce ve MahalleKoy kalıcı koda sahiptir; birleşen, bölünen ya da yeni kurulan birimler eşleştirme tablosuyla bağlanır.
- İlçe kodu şimdilik geçicidir: plaka ve ilçe adı (`34-ESENYURT`). Resmî koda geçiş ileride eşleme/migration ile yapılacak.
- Parti ve bağımsız aday başlıkları `SecimSistemi.Importer/Tanimlar/basliklar.csv` dosyasında tanımlanır. Tanımsız başlık gelirse yükleme durur; başlık tahminle sınıflanmaz. Kontrol: `dotnet run --project SecimSistemi.Importer -- basliklari-denetle data/raw`.
- Yıllar arası karşılaştırma "karşılaştırma birimi"nde yapılır: onaylı mahalle eşlemeleriyle (`SecimSistemi.Importer/Tanimlar/mahalle-eslemeleri.csv`, yalnızca `durum = onayli`) birbirine bağlanan mahalleler tek birimdir. Araç yalnızca öneri yazar; öneriler kullanıcı onaylamadan kullanılmaz.
- DSP yalnızca kendi listesiyle girdiyse "pusulada" sayılır; pusulada değilse oy ve oranlar 0 değil boş (NULL) olur.
- Analiz görünümleri (MahalleKarnesi, SecimDegisimi, AdayEtkisi) migration içinde SQL olarak yazılır. Bağlı oldukları tabloların sütunlarını değiştiren migration görünümleri önce silip sonra yeniden oluşturmalıdır.
- Alınan kararlar gerekçesiyle `docs/kararlar.md` dosyasına yazılır.
- Kodda ve arayüzde "muhtarlık" yerine "mahalle/köy" (`MahalleKoy`) kullanılır. YSK dosyalarındaki "Muhtarlık" sütunu mahalle/köy birimidir, muhtar seçimi değildir.
- Oy türleri: belediye meclisi, il genel meclisi, belediye başkanı, büyükşehir başkanı, milletvekili, cumhurbaşkanı.
- DSP'nin her seçimdeki durumu tutulur: kendi listesi, ittifak, başka partinin listesi, girmedi.
- Yerel seçimde parti gücü meclis oyuyla ölçülür. Aday etkisi = başkanlık oyu − meclis oyu. Cumhurbaşkanlığı sonuçları yalnızca bağlamdır.
- Belediye meclisi ve il genel meclisinde %10 baraj ve D'Hondt uygulanır.

## Veri ve gizlilik (KVKK)
- `data/` klasörü git'e girmez. Ham YSK dosyaları `data/raw` altında değiştirilmeden saklanır.
- Seçmen kütüğü hiçbir zaman depoya girmez; analizde yalnızca sandık ve mahalle toplamları kullanılır.
- Tek tek kişilere siyasi eğilim etiketi yazılmaz.
- Canlı sunucu Türkiye'de olur.

## Çalışma kuralları
- Her veri yüklemesinden sonra toplamlar YSK'nın ilan ettiği sonuçlarla karşılaştırılır.
- Aynı dosya iki kez yüklenince çift kayıt oluşmaz.
- Yeni hesaplama ve gruplama kurallarına xUnit testi yazılır.
- Sık kullanılan komutlar:
  - `dotnet build`
  - `dotnet test`
  - `dotnet run --project SecimSistemi.Web`
  - `dotnet ef migrations add <Ad> --project SecimSistemi.Data --startup-project SecimSistemi.Web`
  - `dotnet run --project SecimSistemi.Importer -- dosya-denetle data/raw` (indirme denetimi; yukle-hepsi de önce bunu çalıştırır)
  - `dotnet run --project SecimSistemi.Importer -- yukle-hepsi data/raw`
  - `dotnet run --project SecimSistemi.Importer -- mahalle listele|esleme-oner|esleme-yukle 34-ESENYURT`
  - `dotnet run --project SecimSistemi.Importer -- rapor erime 34-ESENYURT [--oy-turu belediye-baskani]` (csv: `data/out`, git'e girmez)
  - `dotnet run --project SecimSistemi.Importer -- harita-sinirlari esenyurt` (OSM mahalle sınırları → SecimSistemi.Web/Harita; ODbL, K45)
  - Yeni ilçe ekleme adımları: `docs/yeni-ilce-ekleme.md`
  - PDF için Chromium (her makinede bir kez, `dotnet build` sonrası): `powershell -ExecutionPolicy Bypass -File SecimSistemi.Web/bin/Debug/net10.0/playwright.ps1 install chromium`

## Pilot: İstanbul / Esenyurt
- Pilot ilçe İstanbul/Esenyurt.
- YSK Açık Veri Portalı'ndan ilçe başına, oy türü başına bir dosya iniyor. Her satır bir sandık; mahalle/köy kimliği ve adı her satırda yazılı. Dosyalarda kayıtlı seçmen, oy kullanan, geçerli ve geçersiz oy sütunları yok; yalnızca parti ve bağımsız aday oyları var.
- İstanbul büyükşehir olduğu için il genel meclisi yok. Pilot oy türleri: belediye meclisi, belediye başkanı, büyükşehir başkanı.
- `data/raw` dosya adı düzeni: `yil_yerel_oy-turu_ilce.xlsx` (örnek: `2024_yerel_belediye-meclisi_esenyurt.xlsx`).
- Dosya yapısı: `docs/ysk-dosya-yapisi.md`.

## Plan
- Yapım programı: https://claude.ai/artifact/DYnYkvecaSD9bQ7EuN43TJ
- Önce YSK Açık Veri Portalı'ndaki 2009–2024 verisiyle çalışılır. Mart 2027 planı iptal edildi (2026-11-25, K42).
- **Hedef (2026-11-25):** sistem İstanbul (39 ilçe) ve ardından Kocaeli (12 ilçe) için 6 Aralık 2026'dan önce kullanılır olmalı.
- Sprint 2 (veri yükleyici) tamam. Toplam kontrol dosyaları (`yil_yerel_oy-turu_istanbul-ilceler.xlsx`) henüz yok; okuyucuları dosyalar gelince gerçek yapıya göre yazılacak.
- Sprint 3 (mahalle analizi, erime raporu) tamam. Esenyurt'ta 2014–2019 arasında kurulan 23 mahallenin eşlemesi onay bekliyor; o zamana kadar her mahalle ayrı birim (docs/kararlar.md K6).
- Sprint 4 (sayfalar, Excel ve PDF) tamam: `/karne`, `/mahalle/{ilçe}/{kod}`, `/erime`; dışa aktarma `/disa-aktar/{karne|mahalle|erime}.{xlsx|pdf}`.
  Web Sprint 7'ye kadar yalnızca localhost'tan açılır (K16). Sınır kararı düzeltme ekranı Sprint 7'ye ertelendi (K17).
- Sprint 4 geri bildirimi uygulandı (K25–K31): sınır durumu dönemi gösterir, sütun açıklamaları `SutunSozlugu`'nda
  (sayfada ⓘ, PDF'te "Bu rapor nasıl okunur"), grafikler sunucuda SVG (`SvgCizici`).
- Renkler tek ayarda: `appsettings.json` → `Gorunum:AnaRenk` (DSP logo mavisi `#16c0ed`; yazı tonu bundan hesaplanır) ve
  `Gorunum:IkinciRenk` (logo kırmızısı `#f04248`, şimdilik kullanılmıyor). Logo arayüzde ve PDF'te yok (K32).
- Sprint 5 tamam (2026-11-25): mahalle grupları (K34), meclis hesaplayıcısı (2972 sayılı Kanun, K36), öncelik listesi (K38),
  sayfalar `/gruplar`, `/oncelik`, `/meclis` (K39), Excel ve PDF (K41). Meclis hesabı basın kaynaklı iki örnekle test edildi
  (K40, resmî değil). 2014 meclis doğrulaması, resmî veri gelince ayrı küçük iş. Meclis üye sayıları
  `SecimSistemi.Importer/Tanimlar/meclis-uye-sayilari.csv` (Esenyurt 2024 = 45, doğrulanmadı); resmî sayılar kullanıcıdan gelecek.
- Sunum (6 Aralık 2026, K43): Esenyurt + Şişli, Adalar, Bakırköy. Kod hazır: indirme denetimi, il özeti (`/il`), harita
  (`/harita`, internetsiz). Dosyalar gelince: `dosya-denetle`, `yukle-hepsi`, başlık sınıflandırma, `harita-sinirlari`.
  Resmî meclis sayıları ("Seçilen Belediye Meclisi Üyeleri Listesi") okuyucusu dosya gelince yazılacak.
- Erteleniyor: potansiyel skoru, sandık koordinatları, ikna/mobilizasyon etiketi (K37).
