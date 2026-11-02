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

Referanslar: Data → Domain; Importer → Data; Web → Data; Tests → Domain ve Data.

## Veri modeli kuralları
- Sonuçlar "uzun" biçimde tutulur: bölge, seçim, oy türü, parti ya da aday, oy sayısı. Yeni parti veya seçim tablo yapısını değiştirmez.
- Sonuç hangi düzeyde geldiyse orada saklanır: 2009 sonrası sandık, 1991–2007 ilçe düzeyinde. Karşılaştırmalar iki seçimin ortak olduğu en ince düzeyde yapılır.
- Bölge hiyerarşisi: Il › Ilce › MahalleKoy › Sandik. Il, Ilce ve MahalleKoy kalıcı koda sahiptir; birleşen, bölünen ya da yeni kurulan birimler eşleştirme tablosuyla bağlanır.
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

## Plan
- Yapım programı: https://claude.ai/artifact/DYnYkvecaSD9bQ7EuN43TJ
- Önce YSK Açık Veri Portalı'ndaki 2009–2024 verisiyle çalışılır; 1991–2007 il ve ilçe verisi Mart 2027'den sonra eklenir.
- Şu an: Sprint 0 (kurulum ve çözüm iskeleti).
