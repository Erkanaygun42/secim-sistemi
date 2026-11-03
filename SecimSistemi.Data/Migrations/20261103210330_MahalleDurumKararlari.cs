using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SecimSistemi.Data.Migrations
{
    /// <inheritdoc />
    public partial class MahalleDurumKararlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mahalle eşlemelerinin tek kaynağı artık MahalleDurumKararlari. Eski tabloda veri varsa silinmez, migration durur:
            // önce kayıtlar karar tablosuna aktarılmalı.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MahalleKoyEslemeleri") THEN
                        RAISE EXCEPTION 'MahalleKoyEslemeleri tablosunda kayıt var; silinmeden önce MahalleDurumKararlari tablosuna aktarılmalı.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "MahalleKoyEslemeleri");

            migrationBuilder.CreateTable(
                name: "MahalleDurumKararlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MahalleKoyId = table.Column<int>(type: "integer", nullable: false),
                    Karar = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecimId = table.Column<int>(type: "integer", nullable: true),
                    IliskiliMahalleKoyId = table.Column<int>(type: "integer", nullable: true),
                    EslemeTuru = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    YururlukTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Gerekce = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, collation: "tr-x-icu"),
                    Kaynak = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "tr-x-icu"),
                    TalepEden = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "tr-x-icu"),
                    TalepZamani = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Onaylayan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "tr-x-icu"),
                    KararZamani = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    KararNotu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr-x-icu"),
                    YerineGecenKararId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MahalleDurumKararlari", x => x.Id);
                    table.CheckConstraint("CK_MahalleDurumKararlari_Onay", "(\"Durum\" = 'Talep' AND \"Onaylayan\" IS NULL AND \"KararZamani\" IS NULL AND \"YerineGecenKararId\" IS NULL) OR (\"Durum\" = 'Reddedildi' AND \"Onaylayan\" IS NOT NULL AND \"KararZamani\" IS NOT NULL AND \"YerineGecenKararId\" IS NULL) OR (\"Durum\" = 'Onaylandi' AND \"Onaylayan\" IS NOT NULL AND \"KararZamani\" IS NOT NULL)");
                    table.CheckConstraint("CK_MahalleDurumKararlari_Tur", "(\"Karar\" IN ('Sabit', 'SiniriDegisti') AND \"SecimId\" IS NOT NULL AND \"IliskiliMahalleKoyId\" IS NULL AND \"EslemeTuru\" IS NULL) OR (\"Karar\" = 'Esleme' AND \"IliskiliMahalleKoyId\" IS NOT NULL AND \"EslemeTuru\" IS NOT NULL AND \"IliskiliMahalleKoyId\" <> \"MahalleKoyId\")");
                    table.ForeignKey(
                        name: "FK_MahalleDurumKararlari_MahalleDurumKararlari_YerineGecenKara~",
                        column: x => x.YerineGecenKararId,
                        principalTable: "MahalleDurumKararlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MahalleDurumKararlari_MahalleKoyler_IliskiliMahalleKoyId",
                        column: x => x.IliskiliMahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MahalleDurumKararlari_MahalleKoyler_MahalleKoyId",
                        column: x => x.MahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MahalleDurumKararlari_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MahalleDurumKararlari_Durum",
                table: "MahalleDurumKararlari",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_MahalleDurumKararlari_GecerliDurum",
                table: "MahalleDurumKararlari",
                columns: new[] { "MahalleKoyId", "SecimId" },
                unique: true,
                filter: "\"Karar\" IN ('Sabit', 'SiniriDegisti') AND \"Durum\" = 'Onaylandi' AND \"YerineGecenKararId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MahalleDurumKararlari_GecerliEsleme",
                table: "MahalleDurumKararlari",
                columns: new[] { "MahalleKoyId", "IliskiliMahalleKoyId" },
                unique: true,
                filter: "\"Karar\" = 'Esleme' AND \"Durum\" = 'Onaylandi' AND \"YerineGecenKararId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MahalleDurumKararlari_IliskiliMahalleKoyId",
                table: "MahalleDurumKararlari",
                column: "IliskiliMahalleKoyId");

            migrationBuilder.CreateIndex(
                name: "IX_MahalleDurumKararlari_SecimId",
                table: "MahalleDurumKararlari",
                column: "SecimId");

            migrationBuilder.CreateIndex(
                name: "IX_MahalleDurumKararlari_YerineGecenKararId",
                table: "MahalleDurumKararlari",
                column: "YerineGecenKararId");

            // Kararlar silinmez: yanlış karar, yeni bir kararla yerine geçilerek düzeltilir; geçmiş saklanır.
            migrationBuilder.Sql("""
                CREATE FUNCTION "MahalleDurumKarari_Silinemez"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'MahalleDurumKararlari kayıtları silinemez; yeni bir kararla yerine geçin.';
                END $$;

                CREATE TRIGGER "TR_MahalleDurumKararlari_Silinemez"
                    BEFORE DELETE ON "MahalleDurumKararlari"
                    FOR EACH ROW EXECUTE FUNCTION "MahalleDurumKarari_Silinemez"();

                CREATE TRIGGER "TR_MahalleDurumKararlari_Bosaltilamaz"
                    BEFORE TRUNCATE ON "MahalleDurumKararlari"
                    FOR EACH STATEMENT EXECUTE FUNCTION "MahalleDurumKarari_Silinemez"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_MahalleDurumKararlari_Bosaltilamaz" ON "MahalleDurumKararlari";
                DROP TRIGGER "TR_MahalleDurumKararlari_Silinemez" ON "MahalleDurumKararlari";
                DROP FUNCTION "MahalleDurumKarari_Silinemez"();
                """);

            migrationBuilder.DropTable(
                name: "MahalleDurumKararlari");

            migrationBuilder.CreateTable(
                name: "MahalleKoyEslemeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EskiMahalleKoyId = table.Column<int>(type: "integer", nullable: false),
                    YeniMahalleKoyId = table.Column<int>(type: "integer", nullable: false),
                    GecerlilikTarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    Not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "tr-x-icu"),
                    Tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MahalleKoyEslemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MahalleKoyEslemeleri_MahalleKoyler_EskiMahalleKoyId",
                        column: x => x.EskiMahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MahalleKoyEslemeleri_MahalleKoyler_YeniMahalleKoyId",
                        column: x => x.YeniMahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MahalleKoyEslemeleri_EskiMahalleKoyId_YeniMahalleKoyId",
                table: "MahalleKoyEslemeleri",
                columns: new[] { "EskiMahalleKoyId", "YeniMahalleKoyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MahalleKoyEslemeleri_YeniMahalleKoyId",
                table: "MahalleKoyEslemeleri",
                column: "YeniMahalleKoyId");
        }
    }
}
