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
