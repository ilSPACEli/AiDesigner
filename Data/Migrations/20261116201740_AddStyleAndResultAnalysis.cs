using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiDesigner.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStyleAndResultAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "CatalogItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResultAnalyses",
                columns: table => new
                {
                    SavedResultId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Lighting = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Colors = table.Column<string>(type: "text", nullable: false),
                    Style = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAnalyses", x => x.SavedResultId);
                    table.ForeignKey(
                        name: "FK_ResultAnalyses_SavedResults_SavedResultId",
                        column: x => x.SavedResultId,
                        principalTable: "SavedResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultAnalyses");

            migrationBuilder.DropColumn(
                name: "Style",
                table: "CatalogItems");
        }
    }
}
