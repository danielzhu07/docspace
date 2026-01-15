using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocSpace.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float[]>(
                name: "Embedding",
                table: "Documents",
                type: "real[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Documents");
        }
    }
}
