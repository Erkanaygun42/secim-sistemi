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
