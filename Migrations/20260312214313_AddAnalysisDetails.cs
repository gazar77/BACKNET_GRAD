using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeartCathAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Report",
                table: "AnalysisResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "StenosisPercentage",
                table: "AnalysisResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Report",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "StenosisPercentage",
                table: "AnalysisResults");
        }
    }
}
