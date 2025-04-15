using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Artifacts",
                table: "SavedInfos",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Artifacts",
                table: "SavedInfos");
        }
    }
}
