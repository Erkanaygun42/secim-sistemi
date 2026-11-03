using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SecimSistemi.Data.Migrations
{
    /// <inheritdoc />
    public partial class YuklemeKaydi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Yuklemeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DosyaAdi = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, collation: "tr-x-icu"),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "tr-x-icu"),
                    YuklenmeZamani = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SecimId = table.Column<int>(type: "integer", nullable: false),
                    OyTuru = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IlceId = table.Column<int>(type: "integer", nullable: false),
                    SatirSayisi = table.Column<int>(type: "integer", nullable: false),
                    SandikSayisi = table.Column<int>(type: "integer", nullable: false),
                    MahalleSayisi = table.Column<int>(type: "integer", nullable: false),
                    ToplamOy = table.Column<long>(type: "bigint", nullable: false),
                    DspOyu = table.Column<long>(type: "bigint", nullable: true),
                    KontrolSonucu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KontrolDosyasiSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "tr-x-icu"),
                    Durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Yuklemeler", x => x.Id);
                    table.CheckConstraint("CK_Yuklemeler_Sayilar", "\"SatirSayisi\" >= 0 AND \"SandikSayisi\" >= 0 AND \"MahalleSayisi\" >= 0 AND \"ToplamOy\" >= 0 AND COALESCE(\"DspOyu\", 0) >= 0");
                    table.CheckConstraint("CK_Yuklemeler_Sha256", "\"Sha256\" ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_Yuklemeler_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Yuklemeler_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Yuklemeler_Aktif",
                table: "Yuklemeler",
                columns: new[] { "SecimId", "OyTuru", "IlceId" },
                unique: true,
                filter: "\"Durum\" = 'Aktif'");

            migrationBuilder.CreateIndex(
                name: "IX_Yuklemeler_IlceId",
                table: "Yuklemeler",
                column: "IlceId");

            migrationBuilder.CreateIndex(
                name: "IX_Yuklemeler_Sha256",
                table: "Yuklemeler",
                column: "Sha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Yuklemeler");
        }
    }
}
