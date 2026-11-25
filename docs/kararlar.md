# Kararlar

Projede alınan kararlar ve gerekçeleri. Yeni karar en alta, ilgili bölüme eklenir. Bir karar değişirse eskisi silinmez,
"değişti" notuyla yenisine bağlanır.

## Sprint 3 — Mahalle analizi ve erime raporu (2026-11-03)

### Çalışma kuralları (kullanıcının koyduğu)
- Veritabanı silinmez ve yeniden oluşturulmaz; yalnızca migration uygulanır. `--yenile` kullanılmaz.
- Mahalle eşleme önerileri onaylı sayılmaz. Kullanıcı onaylayana kadar yalnızca aynı Muhtarlık Id'li mahalleler
  aynı birimdir (veritabanında zaten tek kayıttırlar). Diğer her mahalle ayrı bir karşılaştırma birimidir.

### K1. Karşılaştırma birimi = onaylı eşlemelerin bağlı bileşeni
- **Karar:** Onaylı eşlemelerle birbirine bağlanan mahalleler (zincir, bölünme, birleşme) tek birim olur.
  Eşlemesi olmayan mahalle tek başına bir birimdir. Birim kodu birimdeki en küçük Muhtarlık Id'dir.
  Birim adı, üye adlarının Türkçe alfabe sırasıyla " + " ile birleşimidir.
- **Gerekçe:** Bir mahalle bölündüğünde eski ve yeni parçaların ayrı ayrı karşılaştırılması yanıltır. Bütün parçaların
  toplamı ise yıllar boyunca aynı toprağı gösterir. En küçük Id'yi kod yapmak, birimler yeniden kurulduğunda kodun
  değişmemesini sağlar (raporlar ve ileride sayfa bağlantıları bu koda dayanabilir).
- **Uygulama:** `Domain/Analiz/BirimOlusturucu.cs` (union-find), `Importer/Mahalleler/BirimKurucu.cs`. Birimler her
  yüklemeden ve her `mahalle esleme-yukle` komutundan sonra ilçe için baştan kurulur.

### K2. Eşlemeler `Tanimlar/mahalle-eslemeleri.csv` dosyasında, onayla
- **Karar:** Araç yalnızca `oneri` satırı yazar. Veritabanına yalnızca kullanıcının `onayli` yaptığı satırlar gider.
  `ret` satırları yeniden önerilmez. Dosya depoda tutulur (mahalle adları ve kodları kamuya açık idari veridir).
- **Gerekçe:** Bölünme ve birleşme bilgisi ad benzerliğinden çıkarılamaz. Yanlış bir eşleme yıllar arası bütün
  karşılaştırmaları sessizce bozar, eksik bir eşleme ise görünür kalır.

### K3. Öneri eşiği: Jaro-Winkler ≥ 0,80, mahalle başına en çok 3 öneri
- **Karar:** Ad, " MAH." / "KÖYÜ" eki atılıp Türkçe büyük harfe çevrilerek karşılaştırılır.
- **Gerekçe:** Daha düşük eşik, birbirine hiç benzemeyen adları öneriye boğar. Öneriler zaten onaysız
  kullanılmadığı için eşik yalnızca gürültüyü azaltır.
- **Sonuç (Esenyurt):** Yalnızca 2 öneri çıktı, ikisi de büyük olasılıkla yanlış: PINAR → ÇINAR (0,87),
  ORHAN GAZİ → OSMANGAZİ (0,84). Pınar ve Orhan Gazi 2019'da da var ve ilçe oy payları düşmemiş.
  Ad benzerliği bu ilçede bölünmeleri bulmaya yaramıyor; bölünme bilgisi resmî kaynaktan girilmeli.

### K4. Bölünme ipucu: ilçe meclis oy payı en az %25 düşen eski mahalle
- **Karar:** `mahalle listele`, yeni mahallelerin ortaya çıktığı her seçim için, varlığını sürdüren ama ilçe oy payı
  belirgin düşen eski mahalleleri listeler. Hiçbiri kendiliğinden eşleme olmaz.
- **Gerekçe:** Toprak kaybeden mahallenin oyu ve sandığı azalır. Eşlemeyi elle girecek kişiye nereye bakacağını gösterir.
- **Sonuç (Esenyurt, 2014 → 2019):** 20 eski mahallenin 14'ü belirgin küçülmüş. Örnekler: Atatürk (sandık 124 → 25),
  İstiklal (134 → 33), Ardıçlı (62 → 14).

### K5. Eşleme tarihi boş olabilir
- **Karar:** `MahalleKoyEslemesi.GecerlilikTarihi` alanı zorunlu değil.
- **Gerekçe:** Bölünmenin yürürlük tarihi çoğu zaman bilinmiyor. Uydurulmuş bir tarih, bilinmeyen tarihten daha zararlıdır.

### K6. Eşleme onaylanmadan alınan raporun sınırı
- **Durum:** Eşleme girilmediği için 2009'daki "Atatürk" ile 2024'teki "Atatürk" aynı Id'ye sahip, ama aynı toprak değil.
  23 yeni mahallenin 2009 tabanı yok.
- **Karar:** Rapor yine de üretilir. Yeni mahalleler 2009 tabanı boş olarak listelenir. Rapor çıktısı bu sınırı belirtir.
  Oranlar (korunma, beklenen oy) mutlak oylardan daha az etkilenir, ama küçülen mahallenin nüfus yapısı da
  değişmiş olabilir. Kesin yorum için eşlemelerin onaylanması gerekir.

### K7. "Pusulada" = DSP kendi listesiyle girdi
- **Karar:** Yalnızca `DspDurumu.KendiListesi` pusulada sayılır. `Girmedi`, `BaskaPartininListesi` ya da hiç katılım kaydı
  olmaması "pusulada değil" demektir: DSP oyu ve oranları 0 değil boş (NULL) olur. Katılım kaydı önce ilçe düzeyinde,
  yoksa il genelinde aranır (büyükşehir başkanlığı il genelinde tutuluyor). DSP pusuladayken bir birimde DSP sütunu yoksa oy 0 olur.
- **Gerekçe:** "0 oy aldı" ile "seçmenin önünde DSP yoktu" ayrı şeylerdir. Başka partinin listesindeki DSP adaylarının
  oyu DSP'nin oyu olarak sayılamaz.

### K8. Toplam oy ve oran
- **Karar:** Toplam oy = o oy türündeki bütün parti ve bağımsız oylarının toplamı. Oran yüzde (0–100) olarak tutulur.
  Puan farkı yüzde puandır.
- **Gerekçe:** YSK sandık dosyalarında geçerli oy sütunu yok. Parti ve aday oylarının toplamı geçerli oyun en yakın
  karşılığıdır; Sprint 2 sorgusu da aynı tanımı kullanıyor. Kontrol dosyaları gelince ilan edilen geçerli oyla karşılaştırılacak.

### K9. Görünümler SQL'de, erime hesabı C#'ta
- **Karar:** MahalleKarnesi, SecimDegisimi ve AdayEtkisi PostgreSQL görünümüdür (migration: AnalizGorunumleri;
  anahtarsız varlıklar: Data/Gorunumler). Erime raporu `Domain/Analiz/ErimeHesabi.cs` içinde saf C# olarak hesaplanır.
  `Data/Analiz/ErimeRaporuServisi.cs` karne görünümünü okuyup hesabı çağırır.
- **Gerekçe:** Karne, değişim ve aday etkisi basit toplama ve farklardır; SQL'de olmaları pgAdmin'den ve Sprint 4
  sayfalarından doğrudan sorgulanmalarını sağlar. Erime kuralları (son seçim, sıfır taban, kale yüzdeliği) daha çok
  dallanma içerir; C#'ta veritabanı olmadan birim testiyle sınanır. Web ve Importer aynı servisi kullanır.
- **Kural:** Görünümler tablolara bağlıdır. Bu tabloların kullanılan sütunlarını değiştiren her migration önce
  görünümleri silmeli, sonra yeniden oluşturmalıdır.

### K10. Ardışık seçim ve aday etkisi
- **Karar:** "Ardışık" = `Secimler` içindeki yerel seçimlerin tarih sırası (LAG). Birim önceki seçimde yoksa önceki
  değerler boş kalır. Aday etkisi = başkanlık oranı − aynı seçimdeki belediye meclisi oranı (belediye ve büyükşehir
  başkanlığı için ayrı satır); oy farkı da verilir. Meclis satırı olmayan birimde aday etkisi satırı üretilmez.
- **Gerekçe:** CLAUDE.md: yerel seçimde parti gücü meclis oyuyla ölçülür, aday etkisi başkanlık ile meclis arasındaki farktır.

### K11. Erime ayrıntıları
- **Son seçim:** her seride (oy türünde) ayrı belirlenir: o ilçede DSP'nin pusulada olduğu en son yerel seçim.
- **Sıfır taban:** 2009 oranı 0 ise korunma oranı boştur. Beklenen oy 0 olur, kayıp oy negatif çıkabilir
  (DSP tabanın üstünde oy aldı).
- **2009'da pusulada değilse:** taban boştur; korunma, beklenen ve kayıp hesaplanmaz.
- **Sıralama:** kayıp oya göre, en büyük kayıp en üstte. Kaybı hesaplanamayan birimler (tabanı yok) en sonda, koda göre.
  Kullanıcı seçimi.
- **Kaleler:** 2009 belediye meclisi oranına göre en güçlü `ceil(n × 0,2)` birim; n = 2009 oranı olan birim sayısı.
  Eşitlikte küçük kod önce gelir. Grup sabittir; başkanlık ve büyükşehir serilerinde de aynı grup kullanılır. Grubun her
  seçimdeki değeri ağırlıklı orandır (grubun DSP oyu ÷ grubun toplam oyu); grupta pusulada olmayan bir satır varsa boştur.
  Ağırlıklı oran kullanıcı seçimi; küçük birimlerin oynak oranları sonucu çarpıtmasın diye.
- **Gerekçe (beklenen oy):** Esenyurt'ta toplam oy 2009–2024 arasında iki katından fazla arttı. Ham oy farkı bu yüzden
  yanıltır; 2009 oranının bugünkü seçmen sayısında kaç oy etmesi gerektiği asıl ölçüdür.

### K12. Rapor çıktısı
- **Karar:** `rapor erime <ilçe kodu> [--oy-turu ...]` seçilen seriyi ekrana yazar (varsayılan belediye meclisi).
  Üç serinin erime csv'si ve kale csv'si `data/out` altına yazılır. Biçim: UTF-8 BOM, `;` ayraç, ondalıkta virgül.
- **Gerekçe:** Excel tr-TR ayarıyla dosyayı doğrudan doğru sütunlara açar. `data/` git'e girmez, analiz sonuçları depoya girmez.

## Sınır değişikliği tespiti ve insan kararları (2026-11-04)

### K13. Sınır durumu otomatik hesaplanır, eşikler yöne göre ayrıdır
- **Karar:** Referans, en son seçimdeki birimlerdir. Pay = birimin belediye meclisi oyu ÷ ilçe meclis oyu.
  Durumlar: Sabit, SonradanKuruldu (değerler boş), Yeni (ilk göründüğü seçim), Şüpheli, SınırıDeğişti.
  Aynı seçimde ilçede yeni mahalle varsa ve pay en az **%25 düştüyse** ya da en az **%60 arttıysa** sınırı değişti sayılır.
  Pay %15'ten fazla ama eşikten az değiştiyse **Şüpheli** olur. İlçede yeni mahalle yoksa durum sabittir.
  Eşikler `--dusus-esigi`, `--artis-esigi` ve `--supheli-esigi` seçenekleriyle değiştirilebilir.
- **Gerekçe:** İstenen %30 simetrik eşik Esenyurt'ta çalışmadı. Küçüldüğü bilinen Talatpaşa'nın payı yalnızca %27,3 düştü,
  sabit Akçaburgaz'ın payı ise %48,5 arttı (sürekli büyüyen bir mahalle). Hiçbir simetrik eşik ikisini birden doğru ayırmıyor.
  Bölünmede eski mahallenin payı düşer; bu yüzden düşüş eşiği artış eşiğinden küçük tutuldu.
- **Esenyurt sonucu (2014 → 2019):** Küçüldüğü bilinen 14 mahallenin 14'ü de "sınırı değişti" çıktı.
  Diğer 6 mahallenin hiçbiri bu durumu almadı. Bunların 3'ü şüpheli çıktı: Namık Kemal −%21, Yeşilkent +%23, Akçaburgaz +%49.
  Kalan 3'ü sabit çıktı: Merkez, Orhan Gazi, Pınar.
  Bu sonuç bir birim testiyle sabitlendi: `SinirAnaliziTestleri.Esenyurt_Kalibrasyonu`.
  Düşüş eşiği için aralık dar: Namık Kemal −%21,2 ile Talatpaşa −%27,3 arasında.
- **UYARI:** Eşikler tek ilçeyle (Esenyurt) ayarlandı. İl geneline geçince onaylanmış insan kararlarıyla yeniden
  ayarlanacak; bunun aracı `rapor esik-dogrulama` komutudur.

### K14. Karşılaştırılabilir dönem
- **Karar:** Birimin dönemi, son "Yeni" ya da "SınırıDeğişti" seçiminden son seçime kadardır. Şüpheli durum dönemi kesmez,
  raporda ⚠ işaretiyle gösterilir. Mahalle düzeyi erimede her birimin tabanı kendi döneminin ilk seçimidir; 2009 tabanı
  yalnızca 2009'dan beri sabit birimlerde kullanılır. Rapor önce döneme (en uzun dönem önce), sonra kayıp oya göre sıralanır.
- **Kaleler:** Kale grubu yalnızca 2009'dan beri karşılaştırılabilir birimlerden seçilir. Kullanıcıya soruldu, cevap
  gelmedi; önerilen seçenek uygulandı. Esenyurt'ta bu yüzden grup küçük.
- **İlçe düzeyi:** 2009'dan bugüne uzun dönem erime ilçenin tamamı için ayrıca raporlanır; ilçe sınırı bu dönemde değişmedi.

### K15. İnsan kararları tek kaynak (MahalleDurumKararlari)
- **Karar:** Karar türleri: Sabit, SınırıDeğişti (mahalle + seçim) ve Eşleme (mahalle çifti). Durumlar: Talep,
  Onaylandı, Reddedildi. Onaylanmış ve yerine başkası geçmemiş karar geçerlidir ve otomatik tespitin önüne geçer;
  raporda "Şüpheli" yerine "Doğrulandı" görünür. Onaylı eşleme, birime katılan mahallenin kurulduğu seçimde birimi
  doğrulanmış sabit yapar ve dönemi geriye uzatır.
- **Silinmezlik:** Kararlar silinmez; bir veritabanı tetikleyicisi DELETE ve TRUNCATE işlemlerini reddeder. Aynı konuda
  yeni bir karar onaylanınca eski kararın `YerineGecenKararId` alanı dolar ve eski karar geçmişte kalır.
  Bir konuda tek geçerli karar olmasını süzgeçli benzersiz indeksler sağlar.
- **Eşlemeler:** Eski `MahalleKoyEslemeleri` tablosu kaldırıldı; migration, tabloda veri varsa durmak üzere yazıldı.
  `mahalle esleme-yukle --kisi` komutu csv'deki onaylı satırları onaylanmış eşleme kararı olarak aktarır.
  csv artık yalnızca öneri ve aktarım dosyasıdır.
- **Yetki:** TalepEden ve Onaylayan şimdilik serbest metindir. Rol ve yetki Sprint 7'de, düzeltme ekranı Sprint 4'te gelecek.
  **Değişti (2026-11-25):** düzeltme ekranı Sprint 7'ye ertelendi, bkz. K17.

## Sprint 4 — Sayfalar ve dışa aktarma (2026-11-25)

### K16. Sprint 7'ye kadar yalnızca localhost
- **Karar:** Web uygulaması yalnızca bu bilgisayardan açılır. Loopback dışından gelen her istek 403 alır
  (`Program.cs`, ilk ara katman). `AllowedHosts` da `localhost;127.0.0.1;[::1]` ile sınırlıdır. Sunucuya kurulmaz.
- **Gerekçe:** Giriş ve yetki Sprint 7'de gelir. "Partiye özel" analizler girişsiz bir adreste durmamalı. Kullanıcı seçimi.

### K17. Düzeltme ekranı Sprint 7'ye ertelendi (K15 değişti)
- **Karar:** Sınır kararı talep ve onay ekranı Sprint 4'te yapılmadı; Sprint 7'de giriş ve yetkiyle birlikte gelecek.
  O zamana kadar kararlar `karar-talep` ve `karar-onayla` komutlarıyla girilir. Eşlemeler onay bekliyor; otomatik tespit
  şimdilik yeterli.
- **Gerekçe:** Talep eden ve onaylayan rollere bağlıdır. Roller olmadan ekran, herkesin her kararı onaylamasına izin verir.
  Kullanıcı seçimi.

### K18. Sayfalarda sınır eşikleri sabit
- **Karar:** Sayfalar ve dışa aktarılan dosyalar varsayılan eşikleri kullanır (düşüş %25, artış %60, şüpheli %15;
  `KarneServisi.Esikler`). Eşik değiştirme yalnızca komut satırındadır (`--dusus-esigi` ...).
- **Gerekçe:** Aynı mahalle iki ekranda farklı durumda görünmesin. Eşikler insan kararlarıyla yeniden ayarlanınca kodda
  değişir (K13). Kullanıcı seçimi.

### K19. Filtreler ve sıra adreste; dosyalar aynı adresten
- **Karar:** Karne `/karne?ilce=34-ESENYURT&secim=4&oy-turu=belediye-meclisi&sirala=dsp-orani&yon=azalan`,
  erime `/erime?ilce=…&oy-turu=…`, mahalle `/mahalle/34-ESENYURT/119318`. Excel ve PDF bağlantıları
  (`/disa-aktar/{karne|mahalle|erime}.{xlsx|pdf}`) aynı parametreleri taşır ve sayfanın kullandığı hazırlayıcıyı
  (`KarneHazirlayici`, `ErimeHazirlayici`) çağırır. Eksik ya da geçersiz parametre varsayılana döner: ilk ilçe, en son seçim,
  belediye meclisi, DSP oranı azalan. Seçim, adreste kimliğiyle (Id) yazılır; aynı yılda birden çok seçim olabilir.
- **Gerekçe:** Bağlantı paylaşılabilir. Dosyada ekrandakiyle aynı tablo, aynı sırayla çıkar.

### K20. Karnede sınır durumu ve şüpheli uyarısı seçilen yıla göre
- **Karar:** Karnedeki "Dönem", o seçime kadarki son "Yeni" ya da "Sınırı değişti" seçiminden o seçime kadardır
  (`BirimDonemi.DonemBaslangiciYil`). Uyarı, bu dönem içinde insan kararı olmayan bir şüpheli seçim varsa çıkar
  (`SupheliYillar`). Son seçimde bu, K14'teki dönemle aynıdır. Sınır durumu belediye meclisi payından hesaplanır ve
  bütün oy türlerinde aynıdır.
- **Gerekçe:** 2014 karnesinde 2019'daki bir sınır değişikliği önemli değildir; 2024 karnesinde 2019 şüphesi önemlidir.
- **Esenyurt sonucu (2024):** 3 birim uyarılı: Akçaburgaz, Namık Kemal, Yeşilkent.
  **Değişti (2026-11-25):** "Sınır durumu" sütunu artık yalnızca o yılı değil dönemi gösterir; "Dönem" sütununun adı
  "Karşılaştırılabilir dönem" oldu, bkz. K25.

### K21. Karne sıralaması
- **Karar:** Boş değerler (DSP pusulada değil, sınır durumu yok) iki yönde de en sondadır. Eşitlikte ad (Türkçe alfabe),
  sonra kod. Sınır sütununda artan sıra önce dikkat isteyenleri getirir: şüpheli, dönemi şüpheli, sınırı değişti, yeni, sabit.
  Sıralama sunucuda yapılır (`KarneSiralamasi`); ekran, Excel ve PDF aynı sırayı kullanır.
- **Gerekçe:** "Pusulada değil" bir değer değildir; en büyük ya da en küçük oranın arasına karışmamalı.
  **Değişti (2026-11-25):** sınır sırası dönemin sınır özetine göredir (şüpheli, sınırı değişti, yeni, sabit); karneye
  puan farkı sütunu ve sırası eklendi, bkz. K25 ve K29.

### K22. Excel ve PDF ortak belge modelinden
- **Karar:** Raporlar önce `RaporBelgesi`'ne çevrilir (`RaporBelgeleri`), Excel ve PDF bu modelden yazılır. Ekrandaki
  açıklama balonlarının yerine dosyada "Uyarı" sütunu vardır. Notlar sayfada ve dosyada aynı metindir.
  Başlık bilgisi iki dosyada da ilk yerdedir: "PARTİYE ÖZEL — dağıtılmaz" ibaresi, rapor adı, filtre özeti, veri kaynağı ve
  dayandığı yüklenmiş dosyalar (yükleme kaydından), üretim tarihi (Türkiye saati).
  Dayandığı dosyalar: karnede seçilen dosya ile sınır analizinin kullandığı belediye meclisi dosyaları; mahalle ve erimede
  ilçenin bütün yerel seçim dosyaları.
- **Excel:** İlk sayfa "Bilgi". Her tablo ayrı sayfadır: sayılar sayı olarak yazılır, başlık satırı dondurulmuş ve süzgeçlidir,
  toplam satırı süzgecin dışındadır. Boş değer "—" yazılır. Dönem grup satırları Excel'e yazılmaz; aynı bilgi "Dönem"
  sütunundadır, böylece süzgeç ve sıralama bozulmaz. Beklenen ve kayıp oy tam sayıya, oranlar 4 ondalığa yuvarlanır
  (hesap artıkları dosyaya girmesin; ekranda 2 ondalık).
- **Gerekçe:** Tek model, iki çıktının birbirinden ayrışmasını önler.
  **Değişti (2026-11-25):** başlıkta dosya listesi yerine kısa özet var, tam liste sona taşındı; notlara "Bu rapor nasıl
  okunur" bölümü eklendi; Excel'de "Dönem" sütununun adı "Karşılaştırılabilir dönem" oldu, bkz. K26 ve K28.

### K23. PDF: HtmlRenderer + Playwright, sunucu kendine istek atmaz
- **Karar:** `PdfSablonu` (düz HTML, satır içi CSS) HtmlRenderer ile metne çevrilir. Chromium bu metni `SetContentAsync` ile
  yükler ve A4 yatay basar. Her sayfanın altında ibare, rapor adı, üretim tarihi ve sayfa numarası yer alır. Chromium uygulama
  boyunca bir kez açılır (`PdfUretici` singleton); ilk PDF ~3 sn, sonrakiler ~1 sn sürer (Esenyurt).
- **Gerekçe:** Sayfanın adresini açıp basmak, Sprint 7'de girişle birlikte çerez ve oturum taşımayı gerektirirdi.
- **Kurulum:** Chromium her makinede bir kez kurulur (CLAUDE.md). Linux sunucuda Türkçe karakterli bir yazı tipi
  (ör. DejaVu) da kurulmalı; şablon "Segoe UI, Arial, DejaVu Sans" sırasıyla arar.

### K24. Tests → Web referansı
- **Karar:** Test projesi Web'e de başvurur: belge modeli, Excel, PDF şablonu ve gerçek PDF üretimi test edilir.
  Gerçek PDF testi Chromium'un kurulu olmasını ister; kurulu değilse kurulum komutunu yazan bir hatayla düşer.
- **Gerekçe:** Dışa aktarma kuralları (başlık bilgisi, biçimler, sıra) testsiz kalmamalı. Kullanıcı seçimi.

## Sprint 4 geri bildirimi — anlaşılırlık, renk, grafikler (2026-11-25)

### K25. Sınır durumu dönemi gösterir (K20 değişti)
- **Karar:** Karnede ve mahalle detayında "Sınır durumu", o seçime göre karşılaştırılabilir dönemin tamamının özetidir
  (`BirimDonemi.DonemSiniri`): dönem içinde insan kararı olmayan şüpheli seçim varsa "Şüpheli (2019)"; yoksa dönem bir
  kesintiyle başladıysa "Sınırı değişti (2019)" ya da "Yeni (2019)"; yoksa "Sabit". İnsan kararı varsa sonuna ✓ eklenir.
  Uyarı olan satırda bu sütun hiçbir zaman "Sabit" yazmaz. Yalnızca o seçimin tespiti mahalle detayındaki "Sınır geçmişi"
  tablosunda ("O seçimdeki sonuç") durur. Sütun adları: "Karşılaştırılabilir dönem" ve (erime) "2009 kalesi".
- **Gerekçe:** Akçaburgaz 2024'te "Sabit" görünüyor, aynı satırda 2019 şüphesi uyarısı çıkıyordu; okuyan çelişki sanıyordu.
  Kullanıcı isteği.
  **Değişti (2026-11-25):** "2009 kalesi" sütununun ve tablosunun adı "2009 ilk %20" oldu, bkz. K35.

### K26. Sütun açıklamaları tek sözlükte
- **Karar:** Her sütunun adı, sade bir açıklaması ve bir örneği `SutunSozlugu`'nda tutulur. Sayfalarda her sütun başlığının
  yanında ⓘ vardır (üzerine gelince açıklama ve örnek). PDF'te "Bu rapor nasıl okunur" bölümü ve Excel'in Bilgi sayfası
  aynı sözlükten, rapordaki sütunların sırasıyla, her sütun bir kez yazılır. Genel notlar ayrıca kalır.
- **Gerekçe:** Açıklama tek yerde değişir; ekran, PDF ve Excel birbirinden ayrışmaz. Kullanıcı isteği.

### K27. Ana renk tek ayarda: DSP logosundaki mavi
- **Karar:** `appsettings.json` → `Gorunum:AnaRenk` (varsayılan `#21bcec`, DSP logosunun baskı sürümünden ölçüldü).
  Bant ("partiye özel"), üst çubuğun alt çizgisi, PDF başlığı ve Excel'deki ibare ile grafiklerin ilk serisi bu rengi kullanır.
  Logo mavisi açık bir ton: beyaz üzerinde kontrastı 2,2:1, yazı için okunmaz. Bu yüzden butonlar, bağlantılar ve başlık
  yazıları ana rengin koyulaştırılmış tonunu kullanır (aynı ton, beyaz üzerinde en az 5,5:1); bu ton ana renkten hesaplanır,
  ayrıca ayarlanmaz. Bant yazısı koyu lacivert. Üst çubuk beyaz, solda yatay DSP logosu (`wwwroot/img`); PDF'te logo
  derlemeye gömülüdür (sunucuya istek atılmaz). Dikey logo da `wwwroot/img`'de, henüz kullanılmıyor.
- **Grafik renkleri:** logo mavisi, turuncu, mor, sarı; sıra sabit ve renk seriyi izler (meclis her zaman 1. renk).
  Renk körlüğü denetiminden geçti. Logo mavisi ve turuncu beyaz zeminde 3:1'in altında kaldığı için her grafikte değer
  etiketleri ve altında tablo vardır; seriler renkten başka işaret biçimiyle de ayrılır (daire, kare, eşkenar dörtgen).
- **Gerekçe:** Kullanıcı parti rengini istedi ve tonu sonradan tek yerden değiştirebilmek istedi. Kırmızı (eski ana renk)
  kaldırıldı.
  **Değişti (2026-11-25):** ana renk `#16c0ed`, üst çubuğun zemini mavi, logo üst çubuktan ve PDF'ten kaldırıldı;
  logo kırmızısı ikinci renk olarak ayarda, bkz. K32.

### K28. Başlıkta kaynak özeti, dosya listesi sonda (K22 değişti)
- **Karar:** PDF ve Excel başlığında yalnızca "YSK Açık Veri Portalı, sandık düzeyi sonuçlar · 12 dosya · son yükleme
  3 Kasım 2026" yazar. Yüklenen dosyaların tam listesi en sondadır (PDF: notlardan sonra; Excel: Bilgi sayfasının sonu).
- **Gerekçe:** 12 dosyalık liste başlığı kalabalıklaştırıyordu. Kullanıcı isteği.

### K29. Karnede önceki seçime göre puan farkı
- **Karar:** Karneye "Puan farkı (2019'a göre)" sütunu eklendi: DSP oranının bir önceki yerel seçime göre farkı
  (SecimDegisimi görünümü). Sınır tam bu seçimde değiştiyse değer soluk ve ⚠ işaretli, dosyada "Uyarı" sütununda yazılı.
  Önceki seçimde birim yoksa boş. Sütun sıralanabilir (`sirala=puan-farki`).
- **Gerekçe:** Karne tek seçimi gösteriyordu; değişimi görmek için mahalle detayına girmek gerekiyordu. Kullanıcı isteği.

### K30. Erime mahalle tablosunda yıllar yan yana
- **Karar:** Mahalle tablosunda serideki her seçimin oranı (2009, 2014, 2019, 2024) yan yana gösterilir. Mahallenin
  karşılaştırılabilir döneminin dışındaki yıllar boş ve gridir (sayfa, Excel, PDF); değer bilinse de yazılmaz.
  "Taban %" ve "Son %" sütunları kaldırıldı: taban ilk dolu yıl, son ise son yıl sütunudur.
- **Gerekçe:** Erimenin yolu (bir anda mı, yavaş mı) görünür. Dönem dışı değer, aynı toprağı göstermediği için yanıltır.
  Kullanıcı isteği.

### K31. Grafikler sunucuda SVG
- **Karar:** Grafikler (`SvgCizici`) sunucuda SVG metni olarak çizilir; sayfa ve PDF aynı çizimi kullanır, JavaScript ya da
  ek paket yoktur. Üç grafik: erime sayfasında ilçe düzeyinde yıllara göre oran (seri başına bir çizgi) ve seçili seride
  en çok oy kaybeden 10 mahalle (çubuk); mahalle detayında yıllara göre oran (oy türü başına bir çizgi).
  Boş değer (DSP pusulada değil) çizgide boşluktur. Mahalle grafiğinde dönem dışı yıllar gri bantta ve soluk; çizgi dönem
  sınırında kesilir. En çok kaybedenler grafiğinde her mahalle kendi dönemine göredir; dönem etiketin yanında yazılır,
  şüpheli mahallede ⚠ vardır, kaybı olmayanlar girmez. Her grafiğin altında verisini gösteren tablo vardır; Excel'de grafik
  yoktur, tablo vardır.
- **Gerekçe:** PDF sunucuda basıldığı için grafik de sunucuda çizilmeli; JavaScript grafik kütüphanesi PDF'te ayrıca
  beklenmeyi gerektirirdi. Farklı dönemler aynı grafikte K6/K14 gereği yan yana sıralanamaz sayılabilirdi; kullanıcı
  "en çok kaybeden 10 mahalle" istediği için birlikte gösterilir ama dönem her çubuğun etiketindedir.
  **Değişti (2026-11-25):** en çok kaybedenler her dönem için ayrı grafiktir, bkz. K33.

### K32. Üst çubuk parti mavisi, logo yok; ikinci renk ayarda (K27 değişti)
- **Karar:** Ana renk `#16c0ed` (logonun web sürümündeki mavi). Üst çubuğun zemini ana renktir; yazı ve simgeler koyu
  lacivert (`#0b2530`): beyaz bu mavi üzerinde 2,1:1 kontrastla okunmaz, lacivert 7,4:1. "Partiye özel" bandı üst çubukta
  lacivert zemin üzerinde mavi; PDF'te mavi zemin üzerinde lacivert. Logo üst çubuktan ve PDF başlığından kaldırıldı
  (PDF'e gömülü logo da kalktı); logo dosyaları `wwwroot/img`'de duruyor, kullanılmıyor.
  Logodaki kırmızı (`#f04248`) `Gorunum:IkinciRenk` ayarına eklendi ama hiçbir yerde kullanılmıyor.
- **Gerekçe:** Kullanıcı isteği. Kırmızı, uyarı renkleriyle (şüpheli sarısı, "sınırı değişti" kırmızısı) karışmasın.

### K33. En çok kaybedenler dönem başına ayrı grafik (K31 değişti)
- **Karar:** Erime sayfasında "En çok oy kaybeden mahalleler" her karşılaştırılabilir dönem için ayrı grafiktir
  (Esenyurt: 2009–2024 ve 2019–2024), tablodaki dönem gruplamasıyla aynı sırada; her grafik o dönemin en çok 10 mahallesini
  gösterir. Her grafiğin ölçeği kendi en büyük kaybına göredir; değerler çubuk ucunda yazılı.
- **Gerekçe:** Farklı tabanlardan gelen kayıplar tek sıralamada karşılaştırılamaz (K6, K14). Kullanıcı isteği.

## Sprint 5 — Mahalle grupları, öncelik listesi, meclis hesaplayıcısı (2026-11-25)

### K34. Mahalle grupları: kale, erozyon, yükselen, zayıf
- **Karar:** Her mahalle kendi karşılaştırılabilir dönemine göre değerlendirilir (taban = dönemin ilk seçimi; K14), aynı
  oy türünde (`GrupHesabi`):
  - **Güçlüydü:** taban oranı ≥ ilçenin aynı yıldaki oranı. Eşitlik güçlü sayılır.
  - **Eğilim:** oy değişimi (gerçek − beklenen = −kayıp oy) en az 10 oy **ve** oranın göreli değişimi (son ÷ taban − 1) en az
    %30 ise yönüne göre kazandı/kaybetti; ikisinden biri tutmazsa "değişmedi". Eşiğe eşit değer değişmiş sayılır.
    Taban oranı 0 ise göreli değişim tanımsızdır; yalnızca oy eşiğine bakılır.
  - **Grup:** güçlüydü ve kaybetti → Erozyon; güçlüydü, değişmedi ya da kazandı → Kale; zayıftı ve kazandı → Yükselen;
    zayıftı, değişmedi ya da kaybetti → Zayıf.
  - DSP taban ya da son seçimde kendi listesiyle pusulada değilse ya da ilçenin taban yılı oranı yoksa grup boştur (not yazılır).
- **Eşik tek ayarda:** `appsettings.json` → `"Gruplama": { "EnAzOy": 10, "EnAzGoreliDegisim": 0.30 }`. Sayfada "bu eşikle
  kaç mahalle değişmedi" gösterilir.
- **Gerekçe:** Sabit puan eşiği (ilk öneri 1 puan) DSP'nin farklı güçte olduğu ilçelerde aynı işi görmez: Esenyurt'ta
  oranlar %0,1–4 arasında olduğundan 1 puanı yalnızca Akçaburgaz geçiyordu. Göreli eşik zayıf ve güçlü ilçede aynı
  oranda düşüşü aynı sayar. Kullanıcı seçimi.
- **Esenyurt sonucu (2024, belediye meclisi, 43 mahalle):** kale 3, erozyon 14, yükselen 3, zayıf 23; eşik yüzünden
  değişmedi sayılan 16. 2009–2024 döneminde ilçenin 2009 oranının (%0,98) üstünde yalnızca Akçaburgaz vardı; diğer 5 mahalle
  çok kaybetse de "zayıf" grubundadır (2009'daki ilçe oranı, sonradan bölünen güçlü mahalleleri de içerir).
- **2x2 tablo** (sonraki adım) grupla aynı şey değildir: dikey eksen "Bugünkü güç" (son oran − ilçenin son oranı), grupların
  dayandığı güç ise "Dönem başındaki güç". İkisi bilerek farklı tutuldu. Kullanıcı seçimi.

### K35. "2009 kalesi" → "2009 ilk %20"
- **Karar:** Erime raporundaki sabit grup (2009 belediye meclisi oranına göre ilk %20; K11) "2009 ilk %20" adını aldı.
  "Kale" adı K34'teki gruba kaldı.
- **Gerekçe:** İki ayrı kavram aynı adı taşımasın. Kullanıcı seçimi.

### K36. Belediye meclisi dağılımı: 2972 sayılı Kanun
- **Kaynak:** 2972 sayılı Kanun'un mevzuat.gov.tr'deki Word metni (`https://www.mevzuat.gov.tr/MevzuatMetin/1.5.2972.doc`,
  2026-11-25'te indirildi; metindeki son değişiklik notu 13/3/2018 - 7102). PDF metni okunamadı. 5393 m.17 ve 5216 m.12
  meclisin "ilgili kanunda gösterilen esas ve usullere göre" seçildiğini söyler; üye sayısı ve dağıtım 2972'dedir.
- **Metinden doğrulanan kurallar** (`MeclisHesabi`):
  - **Üye sayısı (m.5/b):** nüfusu 10.000'e kadar 9; 10.001–20.000: 11; 20.001–50.000: 15; 50.001–100.000: 25;
    100.001–250.000: 31; 250.001–500.000: 37; 500.001–1.000.000: 45; 1.000.000'dan fazla: 55. "Son genel nüfus sayımı
    sonuçlarına göre."
  - **Kontenjan sayısı (m.10/c):** üye sayısı 9 ve 11 olan beldelerde 1, 15'te 2, 25 ve 31'de 3, 37'de 4, 45'te 5, 55'te 6.
  - **Baraj ve bölme (m.23/a):** "Bir seçim çevresinde kullanılan geçerli oy toplamının onda birine tekabül eden sayı,
    bütün partilerin ve bağımsız adayların aldıkları oy sayısından ayrı ayrı çıkarılır. Bu çıkarmadan sonra geriye oyu
    kalmayan siyasi partiler ve bağımsız adaylar üye tahsisinde hesaba katılmaz." Kalan oylar 1, 2, 3 ... diye bölünür,
    paylar parti ayrımı yapılmadan büyükten küçüğe sıralanır ve üyelikler bu sırayla verilir. **Bölme ham oyla değil kalan
    oyla yapılır** (düz D'Hondt değil); barajı az farkla geçen liste çok küçük paylarla bölmeye girer.
  - **Kontenjan (m.23/a, 3507 ek):** bölme, üye sayısından kontenjan sayısı çıkarılarak yapılır; kontenjan üyeliklerinin
    hepsini "geçerli oyların en çoğunu almış olan siyasi parti" kazanır.
  - **Eşitlik (m.23/a):** son üyelikler için eşit paylar varsa ad çekilir (kura). Hesap kurayı öngöremez: eşitliği bildirir,
    koltuğu toplam oyu fazla olana verir.
  - **Büyükşehir (m.6/a, m.24):** büyükşehir meclisine her ilçe meclisinin beşte biri (artık sayılar alınmaz) seçiliş
    sırasıyla katılır. Hesaplayıcının kapsamı dışında.
- **Varsayımlar (metinde açık değil):**
  - Barajı geçmek için oyun geçerli oyun onda birinden **fazla** olması gerekir (tam onda birde geriye oy kalmaz).
    Metnin sözcüklerinden çıkan yorum.
  - Bağımsız aday tek kişidir, en çok bir üyelik alır.
  - Her parti kontenjan adayı göstermiştir; kontenjanı alan "en çok oy alan siyasi parti" partiler arasından seçilir
    (bağımsızlar arasından değil). İki partinin oyu eşitse kontenjanın kime gittiği metinde yok: ad sırası.
  - Üye sayısı nüfustan hesaplanmaz: YSK'nın hangi nüfus verisini kullandığı metinden doğrulanamadı ve nüfus verisi bizde
    yok. İlan edilen sayı `SecimSistemi.Importer/Tanimlar/meclis-uye-sayilari.csv` dosyasına kaynağıyla elle girilir;
    `durum` alanı `dogrulandi` ya da `dogrulanmadi`.
  - Metnin 2018 sonrasında değişmediği doğrulanmadı.
- **Girdi:** geçerli oy = sandık dosyalarındaki parti ve bağımsız oylarının toplamı (K8). Parti oyları sandık toplamıdır
  (`MeclisServisi`).
- **Ek oy senaryosu:** varsayılan, ek oyun yeni seçmenden gelmesidir (geçerli oy toplamı da artar); "en büyük partiden"
  seçeneğinde toplam değişmez. Gereken en az ek oy ikili aramayla bulunur; hedefin üye sayısı ek oyla azalmaz. Bir eşikte
  birden çok üye gelebilir (en büyük parti olunca kontenjan da gelir). Kullanıcı seçimi.
- **Esenyurt üye sayıları:** 2024 için 45 kabul edildi, `dogrulanmadi` (kullanıcı varsayımı, 2026-11-25). 2009, 2014,
  2019 ve 2024 üye sayıları ve seçim günkü dağılımları kullanıcı tarafından YSK'dan iletilecek. Basında geçen 2024
  dağılımı ("CHP 28, AK Parti 14, MHP 3") seçim sonrası değişiklikleri içerebileceği için doğrulama testine konmadı.

### K37. İkna/mobilizasyon etiketi ertelendi
- **Karar:** Mahallelerin "ikna" ya da "mobilizasyon" önceliği etiketi veri gelene kadar yapılmaz.
- **Gerekçe:** Etiket mahalle düzeyinde katılım verisi ister (kayıtlı seçmen, oy kullanan). YSK sandık dosyalarında bu
  sütunlar yok (`docs/ysk-dosya-yapisi.md`); `OyOzeti` tablosu boş. Veri gelince ele alınacak. Kullanıcı kararı.

### K38. Öncelik listesi ve satır cümlesi
- **Karar:** Her karşılaştırılabilir dönem için ayrı liste (`OncelikHesabi`; K33 ile aynı gerekçe). Sıra: kayıp oy büyükten
  küçüğe (kazanç eksi kayıptır, sona düşer); eşitlikte mahalle büyüklüğü (son seçimdeki toplam oy; seçmen sayısı yok), sonra
  kod. Kayıp oyu hesaplanamayan mahalle listenin sonundadır. Büyüklük sırası ilçenin tamamına göredir ("ilçenin 20. büyük
  mahallesi"). Sıra numarası her dönemde 1'den başlar.
- **Cümle** (`OncelikCumlesi`): tek cümle, sabit kalıp, sayılar satırdan; mahallenin grubuna göre seyir değişir:
  `AKÇABURGAZ MAH.: 2009'da %3,85 ile ilçe oranının (%0,98) üstündeydi ama geriledi (2024'te %0,32); tabanını
  koruyabilseydi 434 oy daha alırdı (ilçenin 20. büyük mahallesi, 12.274 oy); ⚠ 2019 seçiminde sınır şüphesi var.`
  Seyir: erozyon "ama geriledi", kale "ve gücünü korudu/artırdı", yükselen "ama yükseldi", zayıf "ve daha da geriledi /
  ve değişmedi". Sayılara ek getirilmez (okunuşa bağlıdır, yanlış yazılması kolaydır); yalnızca yıllar ek alır (2009'da,
  2014'te). İki ondalıkta eşit görünen mahalle ve ilçe oranı üç ondalıkla yazılır ("altındaydı" ile çelişmesin).
- **Gerekçe:** Kullanıcı isteği: satırın neden öncelikli olduğu tek cümlede okunmalı. Cümle şablondan üretildiği için
  her satırda aynı yapıdadır, sayı uydurmaz.

### K39. Gruplar, öncelik ve meclis sayfaları
- **Adresler:** `/gruplar?ilce=…&oy-turu=…`, `/oncelik?ilce=…&oy-turu=…&grup=erozyon&donem=2009`,
  `/meclis?ilce=…&secim=…&uye=45&ek-oy=50000&kaynak=en-buyuk-parti`. Filtreler adreste (K19); sayfalar `GrupHazirlayici` ve
  `MeclisHazirlayici` üzerinden aynı hesapları kullanır (Excel ve PDF sonraki adımda bunları kullanacak).
- **Gruplar:** dört grubun sayısı, "bu eşikle kaç mahalle değişmedi", 2x2 tablo (satır: bugünkü güç ilçenin üstünde/altında,
  sütun: dönem içi puan farkı < 0 / ≥ 0), noktalı grafik (renk ve işaret mahallenin dönemine göre; uçtaki birkaç mahallenin adı
  yazılır, üst üste binecek ad yazılmaz) ve mahalle tablosu.
- **Öncelik:** grup ve döneme göre süzülür; süzgeçte sıra numarası mahallenin süzülmemiş dönem listesindeki yeridir.
- **Meclis:** üye sayısı tanım dosyasından gelir, sayfada 9–55 arasından değiştirilebilir. Üye sayısı doğrulanmadıysa sayfanın
  başında turuncu uyarı vardır. Tanım dosyasında o yıl yoksa ilçenin en son tanımlı sayısı, o da yoksa 45 kullanılır ve
  "tanım dosyasında yok" diye uyarılır (Esenyurt'ta 2024 dışındaki yıllar). DSP'ye eklenecek oy girilir; kaynak "yeni seçmen"
  (varsayılan) ya da "en büyük parti"; barajı geçmek ve sonraki üç üyelik için gereken ek oy listelenir ("dene" bağlantısı o
  değeri girer). Ek oy en büyük partinin oyundan fazlaysa senaryo hesaplanmaz, nedeni yazılır. DSP o seçimde listesizse
  dağılım gösterilir, ek oy kapalıdır.
- **Tanım dosyası:** Web projesi `SecimSistemi.Importer/Tanimlar/meclis-uye-sayilari.csv` dosyasını çıktı klasörüne kopyalar;
  dosya tek yerde düzenlenir.
- **Sayı biçimi:** puan farkı yuvarlandıktan sonra işaretlenir (−0,004 "0,00" yazılır, "−0,00" değil).

### K40. Meclis hesabının basın kaynaklı doğrulaması (resmî değil)
- **Karar:** `MeclisDogrulamaTestleri` gerçek Esenyurt oylarıyla (sandık toplamları, veritabanından 2026-11-25) iki seçimi
  basında geçen dağılımla karşılaştırır; testler `[Trait("Kaynak", "basın")]` ile "resmî değil, basın" olarak işaretlidir:
  - **2019, 45 üye:** AK Parti listesi 27 (25 AK Parti + 2 MHP), CHP listesi 18 (16 CHP + 2 İYİ Parti), HDP 0. Kaynak (kullanıcının
    ilettiği): https://www.sozcu.com.tr/akp-mhp-grubu-ile-chp-iyi-parti-grubu-arasinda-oylama-krizi-wp6736514
  - **2024, 45 üye:** CHP 28, AK Parti listesi 17 (14 AK Parti + 3 MHP). Kaynak (kullanıcının ilettiği):
    https://www.aydinlik.com.tr/haber/esenyurt-belediyesi-hangi-partiden-kim-yonetiyor-593361
  Hesap ikisini de tutturur. 2019'da HDP %11,12 ile barajı geçer ama 0 üye alır (barajın üstünde 4.880 oy); bu, kalan oyla
  bölmenin (K36) basın sayısıyla örtüştüğünü gösterir. Haber metinleri tarafımızdan okunmadı; sayılar kullanıcıdan.
- **2014:** resmî dağılım bekleniyor; test açık (atlanıyor). Kanunun okunuşuna göre HDP %10,23 ile 0 üye almalı.
- **Sınır:** basın sayıları seçimden sonraki parti değişikliklerini içerebilir ve liste içindeki partileri ayrı sayar; hesap
  listeyi sayar. YSK'nın ilan ettiği resmî dağılım gelince testler onunla değiştirilecek. Üye sayıları (45) da doğrulanmadı.

### K41. Gruplar, öncelik ve meclis için Excel ve PDF
- **Karar:** `/disa-aktar/{gruplar|oncelik|meclis}.{xlsx|pdf}` sayfanın adresindeki filtrelerle aynı hazırlayıcıları kullanır.
  - **Gruplar:** grup sayıları ve eşik, 2x2 tablo (PDF'te noktalı grafikle), mahalle tablosu.
  - **Öncelik:** süzgeçten geçen her dönem ayrı bölüm (Excel'de ayrı sayfa); her satırda neden öncelikli cümlesi; dosya adında
    süzgeç (`oncelik_34-ESENYURT_belediye-meclisi_erozyon_2009`).
  - **Meclis:** dağılım (ek oy varsa "+X oyla üye" sütunu), DSP için gereken ek oy. DSP satırı kalın.
- **Uyarı alanı:** belge modeline `Uyari` eklendi; PDF'te başlık bilgisinin altında turuncu kutu, Excel'de Bilgi sayfasında
  "Uyarı" satırı. Meclis raporunda doğrulanmamış üye sayısı burada yazılır.
- **Vurgu satırı:** yeni satır türü `Vurgu` (kalın, sıranın herhangi bir yerinde); `Toplam` en sonda kalır ve Excel süzgecinin
  dışındadır.

## Öncelik değişikliği: İstanbul ve Kocaeli (2026-11-25)

### K42. Sprint 5 kapandı; yeni hedef 6 Aralık 2026
- **Karar:** Sprint 5 kapatıldı. Yeni hedef: sistemin İstanbul (39 ilçe) ve ardından Kocaeli (12 ilçe) için 6 Aralık 2026'dan
  önce kullanılır olması. Mart 2027 planı iptal. 2014 Esenyurt meclis doğrulaması (K40) resmî veri gelince ayrı küçük bir iş.
- **Erteleniyor:** potansiyel skoru, sandık koordinatları, ikna/mobilizasyon etiketi (K37). Harita ancak veri, yükleyici, üye
  sayıları ve sayfalar bittikten sonra ve tarihe yetişecekse.
- **Genel kural (kullanıcı):** doğrulanmamış veriler tablolarda görünmeye devam eder, ama her yerde uyarıyla işaretlenir.
- **Gerekçe:** Kullanıcı kararı.

### K43. Sunum kapsamı ve indirme denetimi
- **Kapsam (kullanıcı, 2026-11-25):** 6 Aralık'ta sistemi yalnızca kullanıcı kullanacak (sunum). Esenyurt'a ek olarak Şişli,
  Adalar, Bakırköy; 2009, 2014, 2019, 2024; belediye meclisi, belediye başkanı, büyükşehir başkanı. Dosyalar elle indirilir;
  otomatik indirme aracı yazılmaz (YSK kullanım koşullarında otomatik erişime izin ya da yasak yok; Türkiye geneli için YSK'dan
  toplu veri talep edilecek). Bu ilçelerde belde yok. Kocaeli 6 Aralık sonrasına kaldı.
- **İlçe tanımları:** `34-ŞİŞLİ`, `34-ADALAR`, `34-BAKIRKÖY` (dosya adında `sisli`, `adalar`, `bakirkoy`).
- **Denetim (`dosya-denetle <klasör>`, `DosyaDenetimi`):** kontrol listesi = tanımlı ilçeler × yerel seçim yılları × ilin oy
  türleri (İstanbul: 3). Hata: xlsx olmayan dosya, düzene uymayan ad (doğru ad tahmin edilebiliyorsa önerilir: küçük harf,
  Türkçe harfler ASCII), açılamayan dosya, içeriği aynı iki dosya, aynı yılda iki ilçenin ortak mahalle kimliği. Uyarı: eksik
  dosya, aynı ilçe ve yılın dosyalarında farklı sandık sayısı. Dosyada il, ilçe ya da oy türü bilgisi olmadığı için içerikten
  yalnızca dolaylı çelişkiler yakalanır.
- **`yukle-hepsi` önce denetimi çalıştırır:** hata varsa hiçbir dosya yüklenmez; eksik dosyalar bildirilir, mevcutlar yüklenir.

### K44. İl seçimi ve il özeti
- **İl seçimi:** karne, erime, gruplar, öncelik ve meclis sayfalarında ortak il ve ilçe seçicisi (`IlIlceSecici`). İlçe kodu
  ilini içerdiği için adreste ayrı il parametresi yok; il değişince o ilin ilk ilçesi seçilir. Tek il yüklüyken il seçicisi
  gizlidir. Yeni ilçe yüklenince listelerde kendiliğinden görünür.
- **İl özeti (`/il?il=İSTANBUL&oy-turu=…&sirala=kayip`):** ildeki yüklü ilçeler; değerler ilçenin tamamı için erime raporunun
  ilçe tablosuyla aynı hesap (`ErimeHesabi.IlceErimesi`): son seçimde DSP oranı, 2009 oranı, 2009'a göre puan farkı, korunma,
  beklenen ve kayıp oy, sınır şüphesi olan mahalle sayısı. Sıra: kayıp oy azalan (varsayılan), DSP oranı, puan, korunma ya da
  ad; boş değer iki yönde de sonda. Toplam satırı yüklü ilçelerin bütün mahallelerinin toplamıdır. Kayıp oy çubuk grafiği.
  Excel ve PDF: `/disa-aktar/il.{xlsx|pdf}`.
- **Doğrulanmamış, uyarıyla:** ilin bütün ilçeleri yüklü değilse "toplam il geneli değildir" uyarısı (sayfada, PDF'te, Excel'de);
  ilçe sınırlarının 2009'dan beri değişmediği varsayımı notlarda. İllerin ilçe sayısı `IlOzetiHazirlayici.IlceSayilari`'nda
  (İstanbul 39, Kocaeli 12).

