using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SecimSistemi.Data.Migrations
{
    /// <inheritdoc />
    public partial class KarsilastirmaBirimleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "GecerlilikTarihi",
                table: "MahalleKoyEslemeleri",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateTable(
                name: "KarsilastirmaBirimleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IlceId = table.Column<int>(type: "integer", nullable: false),
                    Kod = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, collation: "tr-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KarsilastirmaBirimleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KarsilastirmaBirimleri_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KarsilastirmaBirimiUyeleri",
                columns: table => new
                {
                    MahalleKoyId = table.Column<int>(type: "integer", nullable: false),
                    BirimId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KarsilastirmaBirimiUyeleri", x => x.MahalleKoyId);
                    table.ForeignKey(
                        name: "FK_KarsilastirmaBirimiUyeleri_KarsilastirmaBirimleri_BirimId",
                        column: x => x.BirimId,
                        principalTable: "KarsilastirmaBirimleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KarsilastirmaBirimiUyeleri_MahalleKoyler_MahalleKoyId",
                        column: x => x.MahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KarsilastirmaBirimiUyeleri_BirimId",
                table: "KarsilastirmaBirimiUyeleri",
                column: "BirimId");

            migrationBuilder.CreateIndex(
                name: "IX_KarsilastirmaBirimleri_IlceId_Kod",
                table: "KarsilastirmaBirimleri",
                columns: new[] { "IlceId", "Kod" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KarsilastirmaBirimiUyeleri");

            migrationBuilder.DropTable(
                name: "KarsilastirmaBirimleri");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "GecerlilikTarihi",
                table: "MahalleKoyEslemeleri",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
