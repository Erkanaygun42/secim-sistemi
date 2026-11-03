using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecimSistemi.Data.Migrations
{
    /// <inheritdoc />
    public partial class TurkceSiralama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Secimler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "TamAd",
                table: "Partiler",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KisaAd",
                table: "Partiler",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "HamAd",
                table: "PartiAdEslemeleri",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "MahalleKoyler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Not",
                table: "MahalleKoyEslemeleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Iller",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Kod",
                table: "Ilceler",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Ilceler",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Not",
                table: "IlceEslemeleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Not",
                table: "DspKatilimlari",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IttifakAdi",
                table: "DspKatilimlari",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AdSoyad",
                table: "Adaylar",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                collation: "tr-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Secimler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "TamAd",
                table: "Partiler",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "KisaAd",
                table: "Partiler",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "HamAd",
                table: "PartiAdEslemeleri",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "MahalleKoyler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Not",
                table: "MahalleKoyEslemeleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Iller",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Kod",
                table: "Ilceler",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Ad",
                table: "Ilceler",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Not",
                table: "IlceEslemeleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "Not",
                table: "DspKatilimlari",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "IttifakAdi",
                table: "DspKatilimlari",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldCollation: "tr-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "AdSoyad",
                table: "Adaylar",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldCollation: "tr-x-icu");
        }
    }
}
