using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parcs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequiresGpuToJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresGpu",
                table: "Jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresGpu",
                table: "Jobs");
        }
    }
}
