using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lore.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeDirectoryPathUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_file_sources_path",
                table: "file_sources",
                column: "path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_file_sources_path",
                table: "file_sources");
        }
    }
}
