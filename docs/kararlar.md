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

### K21. Karne sıralaması
- **Karar:** Boş değerler (DSP pusulada değil, sınır durumu yok) iki yönde de en sondadır. Eşitlikte ad (Türkçe alfabe),
  sonra kod. Sınır sütununda artan sıra önce dikkat isteyenleri getirir: şüpheli, dönemi şüpheli, sınırı değişti, yeni, sabit.
  Sıralama sunucuda yapılır (`KarneSiralamasi`); ekran, Excel ve PDF aynı sırayı kullanır.
- **Gerekçe:** "Pusulada değil" bir değer değildir; en büyük ya da en küçük oranın arasına karışmamalı.

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
