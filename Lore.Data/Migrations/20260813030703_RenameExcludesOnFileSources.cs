using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lore.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameExcludesOnFileSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "exclude_pattern",
                table: "file_sources",
                newName: "exclude_extensions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "exclude_extensions",
                table: "file_sources",
                newName: "exclude_pattern");
        }
    }
}
