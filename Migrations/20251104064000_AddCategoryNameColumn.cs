using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookInfoFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ thêm cột CategoryName vào table SearchHistories hiện có
            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "SearchHistories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Populate CategoryName từ Categories table
            migrationBuilder.Sql(@"
                UPDATE ""SearchHistories"" 
                SET ""CategoryName"" = (
                    SELECT c.""Name""
                    FROM ""Categories"" c 
                    WHERE ""SearchHistories"".""CategoryId"" = c.""CategoryId""
                )
                WHERE ""SearchHistories"".""CategoryId"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "SearchHistories");
        }
    }
}