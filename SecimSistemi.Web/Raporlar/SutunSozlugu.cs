namespace SecimSistemi.Web.Raporlar;

/// <summary>Bir sütunun adı, sade bir açıklaması ve bir örneği.</summary>
public sealed record SutunTanimi(string Ad, string Aciklama, string? Ornek = null)
{
    /// <summary>Sayfadaki ⓘ ipucunun metni.</summary>
    public string Ipucu => Ornek is null ? Aciklama : $"{Aciklama} Örnek: {Ornek}";
}

/// <summary>
/// Bütün raporlardaki sütunların tek sözlüğü. Sayfalardaki ⓘ ipuçları, PDF'teki "Bu rapor nasıl okunur" bölümü ve
/// Excel'in Bilgi sayfası buradan beslenir; bir açıklama değişince üçü birlikte değişir.
/// </summary>
public static class SutunSozlugu
{
    public static readonly SutunTanimi Kod = new("Kod",
        "YSK'nın mahalle/köy kodu. Mahallenin adı değişse de kod aynı kalır.");

    public static readonly SutunTanimi Mahalle = new("Mahalle/köy",
        "Mahallenin ya da köyün en son seçimdeki adı.");

    public static readonly SutunTanimi Sandik = new("Sandık",
        "Mahallede o seçimde kurulan sandık sayısı.",
        "48 → mahallede 48 sandık vardı.");

    public static readonly SutunTanimi ToplamOy = new("Toplam oy",
        "Partilere ve bağımsız adaylara verilen oyların toplamı. YSK sandık dosyalarında geçersiz oy olmadığı için oranlar bu toplama göre hesaplanır.",
        "12.274 → mahallede partiler ve adaylar toplam 12.274 oy aldı.");

    public static readonly SutunTanimi DspOyu = new("DSP oyu",
        "DSP'nin aldığı oy. DSP o seçimde kendi listesiyle pusulada değilse boş (—) kalır; 0 yazılmaz.",
        "39 → DSP bu mahallede 39 oy aldı.");

    public static readonly SutunTanimi DspOrani = new("DSP %",
        "DSP oyunun toplam oya oranı, yüzde.",
        "0,32 → her 1.000 oyun yaklaşık 3'ü DSP'nin.");

    public static readonly SutunTanimi PuanFarki = new("Puan farkı",
        "DSP oranının bir önceki yerel seçime göre farkı, yüzde puan. Sınır tam bu seçimde değiştiyse değer soluk ve ⚠ işaretlidir: aynı toprağı karşılaştırmaz.",
        "−0,41 → oran önceki seçime göre 0,41 puan düştü (örneğin %0,73'ten %0,32'ye).");

    public static readonly SutunTanimi Donem = new("Karşılaştırılabilir dönem",
        "Mahalle sınırlarının aynı kaldığı dönem.",
        "2019–2024: mahalle 2019'da kuruldu ya da sınırı değişti, daha eski yıllarla karşılaştırılamaz.");

    public static readonly SutunTanimi Sinir = new("Sınır durumu",
        "Karşılaştırılabilir dönemin tamamındaki sınır durumu, yalnızca o yılın değil. Şüpheli (yıl): o seçimde mahallenin ilçe oyundaki payı belirgin değişti, karar girilmedi. " +
        "Sınırı değişti (yıl) / Yeni (yıl): dönem o seçimde başladı. Sabit: dönem içinde değişiklik yok. ✓: insan kararıyla doğrulandı.",
        "Şüpheli (2019) → 2019'u içeren karşılaştırmalarda dikkatli olun.");

    public static readonly SutunTanimi Uyari = new("Uyarı",
        "Satırdaki sınır uyarısının açıklaması (sayfada ⚠ simgesinin üzerine gelince görünen metin).");

    public static readonly SutunTanimi Seri = new("Seri",
        "Oy türü: belediye meclisi, belediye başkanı ya da büyükşehir başkanı. Partinin gücü meclis oyuyla ölçülür.");

    public static readonly SutunTanimi Yil = new("Yıl", "Seçim yılı.");

    public static readonly SutunTanimi PuanTabana = new("Puan",
        "DSP oranının taban seçimine göre farkı, yüzde puan. Taban, karşılaştırılabilir dönemin ilk seçimidir.",
        "−3,54 → oran tabana göre 3,54 puan düştü.");

    public static readonly SutunTanimi Korunma = new("Korunma",
        "Son oran ÷ taban oranı. 1 oranın korunduğunu, 0,50 yarıya indiğini, 1'in üstü arttığını gösterir.",
        "0,08 → DSP tabandaki oranının yalnızca yüzde 8'ini koruyor.");

    public static readonly SutunTanimi TabanDsp = new("Taban DSP",
        "Taban seçimindeki (karşılaştırılabilir dönemin ilk seçimi) DSP oyu.",
        "Dönem 2009–2024 ise taban 2009'dur.");

    public static readonly SutunTanimi SonDsp = new("Son DSP",
        "Son seçimdeki DSP oyu. Son seçim, DSP'nin o seride kendi listesiyle pusulada olduğu en son yerel seçimdir.");

    public static readonly SutunTanimi SonToplam = new("Son toplam", "Son seçimdeki toplam oy.");

    public static readonly SutunTanimi Beklenen = new("Beklenen",
        "DSP taban oranını koruyabilseydi son seçimde alacağı oy: taban oranı × son seçimdeki toplam oy.",
        "Taban %3,85, son toplam 12.274 → beklenen 473 oy.");

    public static readonly SutunTanimi Kayip = new("Kayıp",
        "Beklenen oy − gerçek oy. Eksi değer, DSP'nin tabanın üstünde oy aldığını gösterir.",
        "Beklenen 473, gerçek 39 → kayıp 434 oy.");

    public static readonly SutunTanimi Kale = new("2009 ilk %20",
        "2009'da DSP'nin en güçlü olduğu mahallelerin ilk %20'si; her seçimde sabit grup olarak izlenir.",
        "evet → mahalle bu gruptadır; grubun yıllara göre oranı \"2009 ilk %20\" tablosunda. " +
        "Mahalle gruplarındaki \"Kale\" ile karıştırılmamalı: o, mahallenin kendi dönemindeki seyridir.");

    public static readonly SutunTanimi YilOrani = new("Yıllara göre DSP %",
        "Her seçimdeki DSP oranı, yüzde. Gri ve boş hücre: o yıl karşılaştırılabilir dönemin dışında (mahalle henüz yoktu ya da sınırı farklıydı).",
        "2009 gri, 2019 %0,40 → mahalle 2019'da kuruldu ya da sınırı değişti; 2009 değeri aynı toprağı göstermez.");

    public static readonly SutunTanimi KaleOrani = new("Grup oranı",
        "2009 ilk %20 grubunun o seçimdeki ağırlıklı oranı: grubun DSP oyu ÷ grubun toplam oyu, yüzde.");

    // ---------------- Mahalle grupları ve öncelik ----------------

    public static readonly SutunTanimi Grup = new("Grup",
        "Mahallenin kendi karşılaştırılabilir dönemindeki seyri. Kale: dönem başında güçlüydü, korudu. Erozyon: güçlüydü, kaybetti. " +
        "Yükselen: zayıftı, kazandı. Zayıf: zayıftı, kazanamadı. Güçlü = dönem başında ilçe oranının üstünde ya da eşit.",
        "Erozyon → 2009'da ilçenin üstündeydi, oranı en az %30 ve en az 10 oy düştü.");

    public static readonly SutunTanimi DonemBasindakiGuc = new("Dönem başındaki güç",
        "Taban oranı − ilçenin aynı yıldaki oranı, puan. Artı: dönem başında ilçe ortalamasının üstündeydi. Grubu bu belirler.",
        "+2,87 → 2009'da ilçe oranından 2,87 puan yüksekti.");

    public static readonly SutunTanimi BugunkuGuc = new("Bugünkü güç",
        "Son oran − ilçenin son seçimdeki oranı, puan. Artı: bugün ilçe ortalamasının üstünde. 2x2 tablonun dikey ekseni.",
        "+0,11 → 2024'te ilçe oranından 0,11 puan yüksek.");

    public static readonly SutunTanimi Egilim = new("Eğilim",
        "Dönem içindeki değişim, gürültü eşiğiyle: oy farkı ve oranın göreli değişimi eşiği aşmazsa \"değişmedi\" (eşik appsettings.json → Gruplama).",
        "Değişmedi → oran %20 düştü ama eşik %30.");

    public static readonly SutunTanimi OyDegisimi = new("Oy değişimi",
        "Gerçek oy − beklenen oy (beklenen = taban oranı × son toplam oy). Eksi: kayıp, artı: kazanç. Kayıp oyun ters işaretlisi.",
        "−434 → tabanını koruyabilseydi 434 oy daha alırdı.");

    public static readonly SutunTanimi GoreliDegisim = new("Göreli değişim",
        "Oranın taban oranına göre değişimi: son ÷ taban − 1. Taban oranı 0 ise boş.",
        "−%92 → oran tabandakinin yüzde 8'ine indi.");

    public static readonly SutunTanimi Neden = new("Neden öncelikli",
        "Satırın tek cümlelik özeti: dönem başındaki ve bugünkü oran, ilçeye göre konumu, kayıp oy, mahallenin büyüklüğü, varsa sınır uyarısı.");

    public static readonly SutunTanimi Buyukluk = new("Büyüklük sırası",
        "Son seçimdeki toplam oya göre ilçedeki sırası (1 = en büyük). Seçmen sayısı YSK dosyalarında yok.",
        "5 → ilçenin 5. büyük mahallesi.");

    // ---------------- İl özeti ----------------

    public static readonly SutunTanimi Ilce = new("İlçe", "Yüklü ilçe. Adına tıklayınca o ilçenin erime raporu açılır.");

    public static readonly SutunTanimi MahalleSayisi = new("Mahalle", "Son seçimde ilçedeki mahalle sayısı.");

    public static readonly SutunTanimi SonYil = new("Son seçim",
        "DSP'nin o oy türünde kendi listesiyle pusulada olduğu en son yerel seçim.");

    public static readonly SutunTanimi TabanOraniIlce = new("2009 DSP %",
        "İlçenin tamamında 2009'daki DSP oranı, yüzde (erimenin tabanı). İlçe sınırının 2009'dan beri değişmediği varsayılır.",
        "0,98 → 2009'da ilçedeki her 1.000 oyun yaklaşık 10'u DSP'nin.");

    public static readonly SutunTanimi SupheliMahalle = new("Şüpheli mahalle",
        "Son seçime göre karşılaştırılabilir döneminde sınır şüphesi olan (sınır kararı girilmemiş) mahalle sayısı. " +
        "Bu mahallelerin yıllar arası karşılaştırması kesin değildir.",
        "3 → üç mahallede sınır kararı bekleniyor.");

    // ---------------- Meclis hesaplayıcısı ----------------

    public static readonly SutunTanimi Liste = new("Liste", "Parti listesi ya da bağımsız aday.");

    public static readonly SutunTanimi MeclisOyu = new("Oy", "Belediye meclisi oyu (ilçedeki bütün sandıkların toplamı).");

    public static readonly SutunTanimi Baraj = new("Baraj",
        "Geçerli oyun onda biri listenin oyundan çıkarılır; geriye oy kalmazsa liste üye alamaz (2972 m.23/a). " +
        "Barajı geçmek için %10'dan fazlası gerekir.",
        "%10,23 → geçti, ama geriye kalan oy azsa yine üye çıkmayabilir.");

    public static readonly SutunTanimi KalanOy = new("Kalan oy",
        "Oy − geçerli oyun onda biri. Üyelikler bu kalan oyların 1, 2, 3 … diye bölünmesiyle dağıtılır (düz D'Hondt değil).",
        "34.971 oy, baraj 34.179 → kalan 792; bölme bu 792 ile yapılır.");

    public static readonly SutunTanimi Bolmeyle = new("Bölmeyle",
        "Kalan oyların bölünmesiyle kazanılan üyelik (üye sayısı − kontenjan kadar üyelik dağıtılır).");

    public static readonly SutunTanimi KontenjanUye = new("Kontenjan",
        "Kontenjan üyeliği; hepsi geçerli oyların en çoğunu alan partiye gider (2972 m.23/a). Sayısı üye sayısına göre (m.10/c): 45 üyede 5.");

    public static readonly SutunTanimi ToplamUye = new("Üye", "Toplam meclis üyeliği: bölmeyle + kontenjan.");

    public static readonly SutunTanimi EkOy = new("Gereken ek oy",
        "DSP'nin o sayıda üyeye ulaşması için gereken en az ek oy. Yeni seçmenden: geçerli oy toplamı da artar. " +
        "En büyük partiden: oy o partiden düşer, toplam değişmez.",
        "+58.520 → DSP 58.520 oy daha alsaydı 1 üye çıkarırdı.");

    public static readonly SutunTanimi Pay = new("İlçe meclis oyundaki pay",
        "Mahallenin belediye meclisi oyunun ilçe toplamındaki payı. Sınır değişikliği bu payın ani değişiminden tespit edilir.",
        "%2,10 → ilçedeki meclis oylarının yüzde 2,1'i bu mahalleden.");

    public static readonly SutunTanimi PayDegisimi = new("Pay değişimi",
        "Payın bir önceki seçime göre göreli değişimi. Yalnızca ilçede yeni mahalle çıkan seçimlerde sınır tespitine kullanılır.",
        "−%83 → mahallenin payı önceki seçime göre yüzde 83 küçüldü; büyük olasılıkla bölündü.");

    public static readonly SutunTanimi OtomatikTespit = new("Otomatik tespit",
        "Pay değişiminden ve eşiklerden çıkan sonuç (insan kararı olmadan).");

    public static readonly SutunTanimi YillikSonuc = new("O seçimdeki sonuç",
        "Yalnızca o seçim için geçerli sonuç; insan kararı varsa otomatik tespitin yerine geçer.");

    public static readonly SutunTanimi InsanKarari = new("İnsan kararı",
        "Onaylanmış sınır kararı ya da eşleme. Boşsa sonuç otomatik tespittir.");
}
