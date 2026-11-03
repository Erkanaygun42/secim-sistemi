using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SecimSistemi.Data.Migrations
{
    /// <inheritdoc />
    public partial class IlkKurulum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Iller",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Iller", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Partiler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KisaAd = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TamAd = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partiler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Secimler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    Tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secimler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ilceler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IlId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ilceler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ilceler_Iller_IlId",
                        column: x => x.IlId,
                        principalTable: "Iller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartiAdEslemeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HamAd = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PartiId = table.Column<int>(type: "integer", nullable: false),
                    GecerliBaslangic = table.Column<DateOnly>(type: "date", nullable: true),
                    GecerliBitis = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartiAdEslemeleri", x => x.Id);
                    table.CheckConstraint("CK_PartiAdEslemeleri_TarihAraligi", "\"GecerliBaslangic\" IS NULL OR \"GecerliBitis\" IS NULL OR \"GecerliBaslangic\" <= \"GecerliBitis\"");
                    table.ForeignKey(
                        name: "FK_PartiAdEslemeleri_Partiler_PartiId",
                        column: x => x.PartiId,
                        principalTable: "Partiler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Adaylar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdSoyad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SecimId = table.Column<int>(type: "integer", nullable: false),
                    OyTuru = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IlId = table.Column<int>(type: "integer", nullable: false),
                    IlceId = table.Column<int>(type: "integer", nullable: true),
                    PartiId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adaylar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adaylar_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Adaylar_Iller_IlId",
                        column: x => x.IlId,
                        principalTable: "Iller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Adaylar_Partiler_PartiId",
                        column: x => x.PartiId,
                        principalTable: "Partiler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Adaylar_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DspKatilimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SecimId = table.Column<int>(type: "integer", nullable: false),
                    OyTuru = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IlId = table.Column<int>(type: "integer", nullable: false),
                    IlceId = table.Column<int>(type: "integer", nullable: true),
                    Durum = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ListePartiId = table.Column<int>(type: "integer", nullable: true),
                    IttifakAdi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DspKatilimlari", x => x.Id);
                    table.CheckConstraint("CK_DspKatilimlari_ListePartisi", "(\"Durum\" = 'BaskaPartininListesi') = (\"ListePartiId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DspKatilimlari_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DspKatilimlari_Iller_IlId",
                        column: x => x.IlId,
                        principalTable: "Iller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DspKatilimlari_Partiler_ListePartiId",
                        column: x => x.ListePartiId,
                        principalTable: "Partiler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DspKatilimlari_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IlceEslemeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EskiIlceId = table.Column<int>(type: "integer", nullable: false),
                    YeniIlceId = table.Column<int>(type: "integer", nullable: false),
                    GecerlilikTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    Tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IlceEslemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IlceEslemeleri_Ilceler_EskiIlceId",
                        column: x => x.EskiIlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IlceEslemeleri_Ilceler_YeniIlceId",
                        column: x => x.YeniIlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MahalleKoyler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IlceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MahalleKoyler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MahalleKoyler_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MahalleKoyEslemeleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EskiMahalleKoyId = table.Column<int>(type: "integer", nullable: false),
                    YeniMahalleKoyId = table.Column<int>(type: "integer", nullable: false),
                    GecerlilikTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    Tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Sandiklar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    No = table.Column<int>(type: "integer", nullable: false),
                    SecimId = table.Column<int>(type: "integer", nullable: false),
                    MahalleKoyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sandiklar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sandiklar_MahalleKoyler_MahalleKoyId",
                        column: x => x.MahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sandiklar_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OyOzetleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KayitliSecmen = table.Column<int>(type: "integer", nullable: true),
                    OyKullanan = table.Column<int>(type: "integer", nullable: true),
                    GecerliOy = table.Column<int>(type: "integer", nullable: true),
                    GecersizOy = table.Column<int>(type: "integer", nullable: true),
                    SecimId = table.Column<int>(type: "integer", nullable: false),
                    OyTuru = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Duzey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IlId = table.Column<int>(type: "integer", nullable: false),
                    IlceId = table.Column<int>(type: "integer", nullable: true),
                    MahalleKoyId = table.Column<int>(type: "integer", nullable: true),
                    SandikId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OyOzetleri", x => x.Id);
                    table.CheckConstraint("CK_OyOzetleri_Duzey", "(\"Duzey\" = 'Il'         AND \"IlceId\" IS NULL     AND \"MahalleKoyId\" IS NULL     AND \"SandikId\" IS NULL) OR\n(\"Duzey\" = 'Ilce'       AND \"IlceId\" IS NOT NULL AND \"MahalleKoyId\" IS NULL     AND \"SandikId\" IS NULL) OR\n(\"Duzey\" = 'MahalleKoy' AND \"IlceId\" IS NOT NULL AND \"MahalleKoyId\" IS NOT NULL AND \"SandikId\" IS NULL) OR\n(\"Duzey\" = 'Sandik'     AND \"IlceId\" IS NOT NULL AND \"MahalleKoyId\" IS NOT NULL AND \"SandikId\" IS NOT NULL)");
                    table.CheckConstraint("CK_OyOzetleri_Negatif", "COALESCE(\"KayitliSecmen\", 0) >= 0 AND COALESCE(\"OyKullanan\", 0) >= 0 AND COALESCE(\"GecerliOy\", 0) >= 0 AND COALESCE(\"GecersizOy\", 0) >= 0");
                    table.ForeignKey(
                        name: "FK_OyOzetleri_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OyOzetleri_Iller_IlId",
                        column: x => x.IlId,
                        principalTable: "Iller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OyOzetleri_MahalleKoyler_MahalleKoyId",
                        column: x => x.MahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OyOzetleri_Sandiklar_SandikId",
                        column: x => x.SandikId,
                        principalTable: "Sandiklar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OyOzetleri_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sonuclar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartiId = table.Column<int>(type: "integer", nullable: true),
                    AdayId = table.Column<int>(type: "integer", nullable: true),
                    Oy = table.Column<int>(type: "integer", nullable: false),
                    SecimId = table.Column<int>(type: "integer", nullable: false),
                    OyTuru = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Duzey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IlId = table.Column<int>(type: "integer", nullable: false),
                    IlceId = table.Column<int>(type: "integer", nullable: true),
                    MahalleKoyId = table.Column<int>(type: "integer", nullable: true),
                    SandikId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sonuclar", x => x.Id);
                    table.CheckConstraint("CK_Sonuclar_Duzey", "(\"Duzey\" = 'Il'         AND \"IlceId\" IS NULL     AND \"MahalleKoyId\" IS NULL     AND \"SandikId\" IS NULL) OR\n(\"Duzey\" = 'Ilce'       AND \"IlceId\" IS NOT NULL AND \"MahalleKoyId\" IS NULL     AND \"SandikId\" IS NULL) OR\n(\"Duzey\" = 'MahalleKoy' AND \"IlceId\" IS NOT NULL AND \"MahalleKoyId\" IS NOT NULL AND \"SandikId\" IS NULL) OR\n(\"Duzey\" = 'Sandik'     AND \"IlceId\" IS NOT NULL AND \"MahalleKoyId\" IS NOT NULL AND \"SandikId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Sonuclar_Oy", "\"Oy\" >= 0");
                    table.CheckConstraint("CK_Sonuclar_PartiYaDaAday", "(\"PartiId\" IS NULL) <> (\"AdayId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Sonuclar_Adaylar_AdayId",
                        column: x => x.AdayId,
                        principalTable: "Adaylar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sonuclar_Ilceler_IlceId",
                        column: x => x.IlceId,
                        principalTable: "Ilceler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sonuclar_Iller_IlId",
                        column: x => x.IlId,
                        principalTable: "Iller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sonuclar_MahalleKoyler_MahalleKoyId",
                        column: x => x.MahalleKoyId,
                        principalTable: "MahalleKoyler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sonuclar_Partiler_PartiId",
                        column: x => x.PartiId,
                        principalTable: "Partiler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sonuclar_Sandiklar_SandikId",
                        column: x => x.SandikId,
                        principalTable: "Sandiklar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sonuclar_Secimler_SecimId",
                        column: x => x.SecimId,
                        principalTable: "Secimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adaylar_IlceId",
                table: "Adaylar",
                column: "IlceId");

            migrationBuilder.CreateIndex(
                name: "IX_Adaylar_IlId",
                table: "Adaylar",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_Adaylar_PartiId",
                table: "Adaylar",
                column: "PartiId");

            migrationBuilder.CreateIndex(
                name: "IX_Adaylar_SecimId_OyTuru_IlId_IlceId_AdSoyad",
                table: "Adaylar",
                columns: new[] { "SecimId", "OyTuru", "IlId", "IlceId", "AdSoyad" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_DspKatilimlari_IlceId",
                table: "DspKatilimlari",
                column: "IlceId");

            migrationBuilder.CreateIndex(
                name: "IX_DspKatilimlari_IlId",
                table: "DspKatilimlari",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_DspKatilimlari_ListePartiId",
                table: "DspKatilimlari",
                column: "ListePartiId");

            migrationBuilder.CreateIndex(
                name: "IX_DspKatilimlari_SecimId_OyTuru_IlId_IlceId",
                table: "DspKatilimlari",
                columns: new[] { "SecimId", "OyTuru", "IlId", "IlceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_IlceEslemeleri_EskiIlceId_YeniIlceId",
                table: "IlceEslemeleri",
                columns: new[] { "EskiIlceId", "YeniIlceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IlceEslemeleri_YeniIlceId",
                table: "IlceEslemeleri",
                column: "YeniIlceId");

            migrationBuilder.CreateIndex(
                name: "IX_Ilceler_IlId_Ad",
                table: "Ilceler",
                columns: new[] { "IlId", "Ad" });

            migrationBuilder.CreateIndex(
                name: "IX_Ilceler_Kod",
                table: "Ilceler",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Iller_Kod",
                table: "Iller",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MahalleKoyEslemeleri_EskiMahalleKoyId_YeniMahalleKoyId",
                table: "MahalleKoyEslemeleri",
                columns: new[] { "EskiMahalleKoyId", "YeniMahalleKoyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MahalleKoyEslemeleri_YeniMahalleKoyId",
                table: "MahalleKoyEslemeleri",
                column: "YeniMahalleKoyId");

            migrationBuilder.CreateIndex(
                name: "IX_MahalleKoyler_IlceId_Ad",
                table: "MahalleKoyler",
                columns: new[] { "IlceId", "Ad" });

            migrationBuilder.CreateIndex(
                name: "IX_MahalleKoyler_Kod",
                table: "MahalleKoyler",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OyOzetleri_IlceId",
                table: "OyOzetleri",
                column: "IlceId");

            migrationBuilder.CreateIndex(
                name: "IX_OyOzetleri_IlId",
                table: "OyOzetleri",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_OyOzetleri_MahalleKoyId",
                table: "OyOzetleri",
                column: "MahalleKoyId");

            migrationBuilder.CreateIndex(
                name: "IX_OyOzetleri_SandikId",
                table: "OyOzetleri",
                column: "SandikId");

            migrationBuilder.CreateIndex(
                name: "IX_OyOzetleri_Tekil",
                table: "OyOzetleri",
                columns: new[] { "SecimId", "OyTuru", "IlId", "IlceId", "MahalleKoyId", "SandikId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_PartiAdEslemeleri_HamAd_GecerliBaslangic",
                table: "PartiAdEslemeleri",
                columns: new[] { "HamAd", "GecerliBaslangic" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_PartiAdEslemeleri_PartiId",
                table: "PartiAdEslemeleri",
                column: "PartiId");

            migrationBuilder.CreateIndex(
                name: "IX_Partiler_KisaAd",
                table: "Partiler",
                column: "KisaAd",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sandiklar_MahalleKoyId",
                table: "Sandiklar",
                column: "MahalleKoyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sandiklar_SecimId_MahalleKoyId_No",
                table: "Sandiklar",
                columns: new[] { "SecimId", "MahalleKoyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Secimler_Tarih",
                table: "Secimler",
                column: "Tarih",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_AdayId",
                table: "Sonuclar",
                column: "AdayId");

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_IlceId",
                table: "Sonuclar",
                column: "IlceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_IlId",
                table: "Sonuclar",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_MahalleKoyId",
                table: "Sonuclar",
                column: "MahalleKoyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_PartiId_SecimId_OyTuru",
                table: "Sonuclar",
                columns: new[] { "PartiId", "SecimId", "OyTuru" });

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_SandikId",
                table: "Sonuclar",
                column: "SandikId");

            migrationBuilder.CreateIndex(
                name: "IX_Sonuclar_Tekil",
                table: "Sonuclar",
                columns: new[] { "SecimId", "OyTuru", "IlId", "IlceId", "MahalleKoyId", "SandikId", "PartiId", "AdayId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DspKatilimlari");

            migrationBuilder.DropTable(
                name: "IlceEslemeleri");

            migrationBuilder.DropTable(
                name: "MahalleKoyEslemeleri");

            migrationBuilder.DropTable(
                name: "OyOzetleri");

            migrationBuilder.DropTable(
                name: "PartiAdEslemeleri");

            migrationBuilder.DropTable(
                name: "Sonuclar");

            migrationBuilder.DropTable(
                name: "Adaylar");

            migrationBuilder.DropTable(
                name: "Sandiklar");

            migrationBuilder.DropTable(
                name: "Partiler");

            migrationBuilder.DropTable(
                name: "MahalleKoyler");

            migrationBuilder.DropTable(
                name: "Secimler");

            migrationBuilder.DropTable(
                name: "Ilceler");

            migrationBuilder.DropTable(
                name: "Iller");
        }
    }
}
