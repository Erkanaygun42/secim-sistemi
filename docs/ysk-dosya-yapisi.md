# YSK dosya yapısı — İstanbul / Esenyurt pilotu

Kaynak: YSK Açık Veri Portalı, `data/raw` altındaki 12 dosya (2009, 2014, 2019, 2024 yerel seçimleri × belediye meclisi, belediye başkanı, büyükşehir başkanı).
İnceleme tarihi: 2026-11-03. Dosyalar salt okunur açıldı; incelemeden önce ve sonra alınan SHA256 değerleri aynı.

## Kısa özet

- Her dosyada **tek sayfa** var (`Sheet1`), **tek başlık satırı** var (1. satır) ve birleştirilmiş hücre yok. Veri 2. satırda başlıyor.
- **Her satır bir sandık.** İlk üç sütun bütün dosyalarda aynı: `Muhtarlık Id`, `Muhtarlık Adı`, `Sandık No`.
- 4. sütundan itibaren **her parti ya da bağımsız aday ayrı bir sütun** ("geniş" biçim). Hücrelerde yalnızca oy sayısı var.
- **Toplam veya oran satırı yok, toplam veya oran sütunu da yok.** Kayıtlı seçmen, oy kullanan, geçerli ve geçersiz oy bilgisi dosyalarda bulunmuyor.
- Boş hücre ve sayı dışı değer hiçbir dosyada yok.

## Dosya başına ölçüler

| Yıl | Oy türü | Veri satırı (sandık) | Mahalle | Parti/aday sütunu | Toplam oy* | DSP oyu | Sandık No aralığı |
|---|---|---:|---:|---:|---:|---:|---|
| 2009 | Belediye meclisi | 784 | 20 | 9 | 189.194 | 1.853 | 1001–2199 |
| 2009 | Belediye başkanı | 784 | 20 | 11 | 189.821 | 1.301 | 1001–2199 |
| 2009 | Büyükşehir başkanı | 784 | 20 | 26 | 190.237 | 1.384 | 1001–2199 |
| 2014 | Belediye meclisi | 1.313 | 20 | 18 | 341.786 | 341 | 1001–2598 |
| 2014 | Belediye başkanı | 1.313 | 20 | 14 | 344.007 | 311 | 1001–2598 |
| 2014 | Büyükşehir başkanı | 1.313 | 20 | 32 | 344.640 | 237 | 1001–2598 |
| 2019 | Belediye meclisi | 1.631 | 43 | 10 | 436.869 | 1.353 | 1001–2760 |
| 2019 | Belediye başkanı | 1.631 | 43 | 9 | 441.451 | 1.341 | 1001–2760 |
| 2019 | Büyükşehir başkanı | 1.631 | 43 | 32 | 437.003 | 1.045 | 1001–2760 |
| 2024 | Belediye meclisi | 1.869 | 43 | 23 | 463.488 | 975 | 1001–4470 |
| 2024 | Belediye başkanı | 1.869 | 43 | 23 | 464.061 | 973 | 1001–4470 |
| 2024 | Büyükşehir başkanı | 1.869 | 43 | 49 | 460.131 | 516 | 1001–4470 |

\* Toplam oy, parti ve aday sütunlarının toplamıdır. Dosyada ayrıca bir "geçerli oy" sütunu olmadığı için bu toplamın YSK'nın ilan ettiği geçerli oyla tutup tutmadığı henüz kontrol edilmedi.

- Aynı yılın üç dosyasında sandık sayısı ve mahalle sayısı aynı. Büyükşehir başkanı dosyası yalnızca Esenyurt sandıklarını içeriyor; dosyanın büyük olmasının sebebi sütun sayısı (adayların çokluğu).
- Mahalle sayısı 2014 ile 2019 arasında 20'den 43'e çıkmış. Mahalleler bölünmüş ya da yeniden düzenlenmiş; eşleştirme tablosu gerekiyor.

## Mahalle/köy bilgisi

- `Muhtarlık Id`: sayısal kimlik (örnek `119319`, `147450`). 2009 ve 2014'te kimlikler `1193xx` ile başlıyor; 2019'da `1474xx` kimlikler ortaya çıkıyor. Bazı kimlikler yıllar boyunca aynı kalmış: `119334 YEŞİLKENT MAH.` hem 2014'te hem 2019'da var. Kimliğin YSK'nın kalıcı mahalle kodu olup olmadığı Sprint 1'de doğrulanmalı.
- `Muhtarlık Adı`: büyük harf, Türkçe karakterli, `MAH.` ekiyle (örnek `ARDIÇLI MAH.`, `ŞEHİTLER MAH.`).
- Dosyalarda il, ilçe veya köy/mahalle türü sütunu yok. İl ve ilçe bilgisi yalnızca dosya adından (`..._esenyurt.xlsx`) geliyor.
- `Sandık No`: dört haneli sayı. Yıl içinde ve mahalle içinde tekrar etmiyor, ama numaralar ardışık değil ve yıldan yıla aynı sandığı göstermiyor. Sandık seçimden seçime eşleştirilemez; karşılaştırmalar mahalle düzeyinde yapılmalı.

## Parti ve aday sütunları

- Sütun başlıkları parti kısaltması (`AK PARTİ`, `CHP`, `DSP`, `SAADET`, `ZAFER PARTİSİ`, …) veya bağımsız adayın adı-soyadı (`MEHMET ŞÜKRÜ ENGİN`).
- **Parti sütunu ile aday sütununu ayıran bir işaret yok.** Sıralama oy pusulasındaki gibi: önce partiler, en sonda bağımsızlar. Importer bunları bilinen parti adları listesiyle ayırmalı; listede olmayan başlık "bağımsız aday" sayılmalı ya da kullanıcıya sorulmalı.
- Belediye meclisi dosyalarında da bağımsız aday sütunları var (2014: 2, 2024: 2).
- Büyükşehir başkanı dosyalarında İstanbul geneli bağımsız adaylar da var (2024'te 26 aday sütunu); çoğunun Esenyurt'taki oyu 0 ya da 0'a yakın.
- **Sütun sırası her dosyada farklı.** Aynı yılın meclis ve başkan dosyalarında bile farklı. Importer sütun numarasıyla değil, başlık adıyla çalışmalı.

### Yıldan yıla değişen adlar (eşleştirme tablosu gerekecek)

| Durum | Örnek | Not |
|---|---|---|
| Aynı kısaltma, farklı parti | `İP` (2014) ↔ `İYİ PARTİ` (2024) | 2014'teki `İP` İşçi Partisi'dir (bugünkü Vatan Partisi), İYİ Parti değil. İYİ Parti 2017'de kuruldu. |
| Ad değişikliği | `İP` (2014) → `VATAN PARTİSİ` (2019, 2024) | Aynı parti çizgisi |
| Benzer ad, ayrı kayıt | `ANAVATAN` (2009) ↔ `ANAP` (2024) | Aynı parti sayılıp sayılmayacağına karar verilmeli |
| Ardıl partiler | `DTP` (2009) → `HDP` (2014, 2019) → `DEM Parti` (2024) | Ayrı parti olarak saklanmalı; analizde "parti çizgisi" grubu kullanılabilir |
| Yazım farkı | `DEM Parti` küçük harfli, diğerleri büyük | Karşılaştırmadan önce büyük harfe çevirip boşlukları kırpmak gerekiyor |
| Kısaltmalar karışabilir | `ABP` ve `AB PARTİ` 2024'te iki ayrı sütun | Ayrı partiler; birleştirilmemeli |

### Bazı dosyalarda olmayan partiler

- 2019'da üç dosyanın hiçbirinde `MHP` ve `İYİ PARTİ` sütunu yok (ittifak dönemi). `HDP` yalnızca belediye meclisinde var.
- 2024'te `DEM Parti` belediye meclisi ve belediye başkanı dosyalarında yok, yalnızca büyükşehir başkanında var.
- Bu durum DSP için tuttuğumuz "kendi listesi / ittifak / başka partinin listesi / girmedi" bilgisinin başka partiler için de gerekebileceğini gösteriyor. Aksi halde bir partinin oyu "0" ile "girmedi" arasında karışır.

## DSP

DSP her dosyada ayrı bir sütun olarak var; 12 dosyanın hepsinde kendi listesiyle ve adayıyla girmiş görünüyor. Meclis oyları: 2009 1.853, 2014 341, 2019 1.353, 2024 975. Bu rakamlar YSK'nın ilan ettiği sonuçlarla henüz karşılaştırılmadı.

## 2024 belediye meclisi ve belediye başkanı dosyaları

Boyutları neredeyse aynı (1.317.781 ve 1.317.726 bayt), ama **aynı dosya değiller**:
- SHA256 değerleri farklı.
- Hücre hücre karşılaştırmada 28.362 hücre farklı çıktı.
- Başlıklar farklı: meclis dosyasında `SOL PARTİ`, `HKP` ve `TİP` var, başkan dosyasında `MİLLİ YOL` var. Bağımsız adaylar da farklı.
- Oylar farklı (örneğin ilk sandıkta AK PARTİ meclis 91, başkan 93).

Boyutların yakın olmasının sebebi, ikisinin de 1.870 satır × 26 sütun olması.

## Veri kalitesi uyarıları

- Bazı dosyalarda **farklı sandıklara ait, oyları birebir aynı satırlar** var. En şüphelisi 2009 belediye meclisi: yan yana dört çift satır aynı (376–377, 463–464, 574–575, 784–785). Yan yana duran iki sandığın dokuz partide tamamen aynı oyu alması pek olası değil. Bu, YSK kaynağında bir kopyalama hatasına işaret edebilir. Diğer dosyalarda da birkaç aynı satır grubu var; bunların çoğu yan yana değil ve oyları düşük sandıklar, tesadüf olabilir.
- 2009 belediye meclisi 3. satırda (Ardıçlı, sandık 1002) değerler bir sütun kaymış gibi görünüyor: CHP 0, DSP 63. Aynı sandıkta başkan oyu CHP 61, DSP 1.
- Bu satırlar Sprint 1'deki yükleme doğrulamasında (YSK'nın ilan ettiği toplamlarla karşılaştırma) ayrıca kontrol edilmeli.

## Importer için çıkarımlar (Sprint 1)

1. Başlığı 1. satırdan, adla okumak gerekiyor; sütun sırasına güvenilmemeli.
2. İlk üç sütun sabit; geri kalan her sütun "uzun" biçimde bir `Sonuc` satırına dönüşür: mahalle, sandık, seçim, oy türü, parti/aday, oy.
3. Seçim yılı, oy türü ve ilçe bilgisi dosya adından alınır (`yil_yerel_oy-turu_ilce.xlsx`).
4. Parti adı eşleştirme tablosu gerekiyor: ham başlık → `Parti`. Tablo büyük harfe çevirme ve boşluk kırpmayla birlikte kullanılacak; `İP` ve `İYİ PARTİ` gibi tuzaklara dikkat edilmeli.
5. Parti olmayan başlıklar bağımsız aday olarak kaydedilir.
6. Mahalle eşleştirme tablosu gerekiyor (2014'te 20, 2019'da 43 mahalle).
7. Geçerli, geçersiz ve katılım bilgisi bu dosyalarda yok. "YSK'nın ilan ettiği toplamlarla karşılaştırma" için ilçe toplamları ayrı bir kaynaktan girilmeli (ör. YSK'nın ilçe sonuç tutanakları).
8. Yükleme sırasında tekrar eden ve birebir aynı oy satırları raporlanmalı, ama otomatik silinmemeli.
