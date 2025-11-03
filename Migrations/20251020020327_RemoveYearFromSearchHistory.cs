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
            // Check if column exists before dropping
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'SearchHistories' AND column_name = 'Year'
                    ) THEN
                        ALTER TABLE ""SearchHistories"" DROP COLUMN ""Year"";
                    END IF;
                END $$;
            ");
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
