using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookInfoFinder.Migrations
{
    /// <inheritdoc />
    public partial class RemoveYearFromSearchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Year",
                table: "SearchHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "SearchHistories",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SearchHistories",
                keyColumn: "SearchHistoryId",
                keyValue: 1,
                column: "Year",
                value: null);

            migrationBuilder.UpdateData(
                table: "SearchHistories",
                keyColumn: "SearchHistoryId",
                keyValue: 2,
                column: "Year",
                value: null);
        }
    }
}
