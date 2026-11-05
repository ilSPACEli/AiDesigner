using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AiDesigner.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImageFile = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MainColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    SuitableLightings = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LicenseSource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CatalogItemId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ThumbnailData = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedResults_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedResults_CatalogItemId",
                table: "SavedResults",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedResults_CreatedAt_Id",
                table: "SavedResults",
                columns: new[] { "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedResults");

            migrationBuilder.DropTable(
                name: "CatalogItems");
        }
    }
}
