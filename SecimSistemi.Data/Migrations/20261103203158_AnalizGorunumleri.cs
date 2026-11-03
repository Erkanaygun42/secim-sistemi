using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecimSistemi.Data.Migrations
{
    /// <summary>
    /// Analiz görünümleri: MahalleKarnesi, SecimDegisimi, AdayEtkisi. Anahtarsız varlıkları: Data/Gorunumler.
    /// DİKKAT: Bu görünümler Sonuclar, KarsilastirmaBirimleri, KarsilastirmaBirimiUyeleri, Partiler, Secimler ve
    /// DspKatilimlari tablolarına bağlıdır. Bu tabloların kullanılan sütunlarını değiştiren bir migration önce
    /// görünümleri silmeli (DROP VIEW), sonra yeniden oluşturmalıdır.
    /// </summary>
    public partial class AnalizGorunumleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seçim × oy türü × karşılaştırma birimi. Toplam oy = bütün parti ve bağımsız oyları.
            // DSP yalnızca kendi listesiyle girdiyse pusuladadır; önce ilçe düzeyi, yoksa il geneli katılım kaydı kullanılır.
            // Pusulada değilse DspOyu ve DspOrani boş (NULL) kalır; pusuladaysa DSP sütunu olmayan birimde 0 olur.
            migrationBuilder.Sql("""
                CREATE VIEW "MahalleKarnesi" AS
                WITH birim_oy AS (
                    SELECT r."SecimId", r."OyTuru", r."IlId",
                           b."IlceId", b."Id" AS "BirimId", b."Kod" AS "BirimKod", b."Ad" AS "BirimAdi",
                           COUNT(DISTINCT r."SandikId")::int AS "SandikSayisi",
                           SUM(r."Oy")::bigint AS "ToplamOy",
                           SUM(r."Oy") FILTER (WHERE p."KisaAd" = 'DSP')::bigint AS "DspHamOyu"
                    FROM "Sonuclar" r
                    JOIN "KarsilastirmaBirimiUyeleri" u ON u."MahalleKoyId" = r."MahalleKoyId"
                    JOIN "KarsilastirmaBirimleri" b ON b."Id" = u."BirimId"
                    LEFT JOIN "Partiler" p ON p."Id" = r."PartiId"
                    GROUP BY r."SecimId", r."OyTuru", r."IlId", b."IlceId", b."Id", b."Kod", b."Ad"
                )
                SELECT o."SecimId", s."Tarih", EXTRACT(YEAR FROM s."Tarih")::int AS "Yil", s."Tur" AS "SecimTuru",
                       o."OyTuru", o."IlceId", o."BirimId", o."BirimKod", o."BirimAdi", o."SandikSayisi", o."ToplamOy",
                       COALESCE(k."Durum" = 'KendiListesi', FALSE) AS "PusuladaMi",
                       CASE WHEN k."Durum" = 'KendiListesi' THEN COALESCE(o."DspHamOyu", 0) END AS "DspOyu",
                       CASE WHEN k."Durum" = 'KendiListesi' AND o."ToplamOy" > 0
                            THEN 100.0 * COALESCE(o."DspHamOyu", 0) / o."ToplamOy" END AS "DspOrani"
                FROM birim_oy o
                JOIN "Secimler" s ON s."Id" = o."SecimId"
                LEFT JOIN LATERAL (
                    SELECT d."Durum"
                    FROM "DspKatilimlari" d
                    WHERE d."SecimId" = o."SecimId" AND d."OyTuru" = o."OyTuru" AND d."IlId" = o."IlId"
                      AND (d."IlceId" = o."IlceId" OR d."IlceId" IS NULL)
                    ORDER BY d."IlceId" NULLS LAST
                    LIMIT 1
                ) k ON TRUE;
                """);

            // Ardışık iki yerel seçim: Secimler'deki yerel seçimler tarih sırasıyla dizilir, her biri bir öncekiyle eşlenir.
            // Birim önceki seçimde yoksa önceki değerler boş kalır.
            migrationBuilder.Sql("""
                CREATE VIEW "SecimDegisimi" AS
                WITH yerel AS (
                    SELECT "Id", LAG("Id") OVER (ORDER BY "Tarih") AS "OncekiId"
                    FROM "Secimler"
                    WHERE "Tur" = 'Yerel'
                )
                SELECT k."IlceId", k."BirimId", k."BirimKod", k."BirimAdi", k."OyTuru",
                       y."OncekiId" AS "OncekiSecimId", EXTRACT(YEAR FROM ps."Tarih")::int AS "OncekiYil",
                       k."SecimId", k."Yil",
                       o."ToplamOy" AS "OncekiToplamOy", k."ToplamOy",
                       o."DspOrani" AS "OncekiOran", k."DspOrani" AS "Oran", k."DspOrani" - o."DspOrani" AS "PuanFarki",
                       o."DspOyu" AS "OncekiDspOyu", k."DspOyu", k."DspOyu" - o."DspOyu" AS "OyFarki"
                FROM "MahalleKarnesi" k
                JOIN yerel y ON y."Id" = k."SecimId" AND y."OncekiId" IS NOT NULL
                JOIN "Secimler" ps ON ps."Id" = y."OncekiId"
                LEFT JOIN "MahalleKarnesi" o
                       ON o."SecimId" = y."OncekiId" AND o."BirimId" = k."BirimId" AND o."OyTuru" = k."OyTuru";
                """);

            // Aday etkisi = başkanlık oranı − belediye meclisi oranı (aynı seçim, aynı birim).
            migrationBuilder.Sql("""
                CREATE VIEW "AdayEtkisi" AS
                SELECT b."SecimId", b."Tarih", b."Yil", b."IlceId", b."BirimId", b."BirimKod", b."BirimAdi",
                       b."OyTuru" AS "BaskanlikTuru",
                       m."DspOrani" AS "MeclisOrani", b."DspOrani" AS "BaskanlikOrani",
                       b."DspOrani" - m."DspOrani" AS "AdayEtkisiPuan",
                       m."DspOyu" AS "MeclisOyu", b."DspOyu" AS "BaskanlikOyu", b."DspOyu" - m."DspOyu" AS "OyFarki"
                FROM "MahalleKarnesi" b
                JOIN "MahalleKarnesi" m
                  ON m."SecimId" = b."SecimId" AND m."BirimId" = b."BirimId" AND m."OyTuru" = 'BelediyeMeclisi'
                WHERE b."OyTuru" IN ('BelediyeBaskani', 'BuyuksehirBaskani');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP VIEW "AdayEtkisi";""");
            migrationBuilder.Sql("""DROP VIEW "SecimDegisimi";""");
            migrationBuilder.Sql("""DROP VIEW "MahalleKarnesi";""");
        }
    }
}
