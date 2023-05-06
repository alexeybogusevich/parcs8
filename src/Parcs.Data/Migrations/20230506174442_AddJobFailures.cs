using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Parcs.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobStatusEntity_Jobs_JobId",
                table: "JobStatusEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobStatusEntity",
                table: "JobStatusEntity");

            migrationBuilder.RenameTable(
                name: "JobStatusEntity",
                newName: "JobStatuses");

            migrationBuilder.RenameColumn(
                name: "JobStatus",
                table: "JobStatuses",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_JobStatusEntity_JobId",
                table: "JobStatuses",
                newName: "IX_JobStatuses_JobId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobStatuses",
                table: "JobStatuses",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "JobFailures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    StackTrace = table.Column<string>(type: "text", nullable: false),
                    CreateDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobFailures_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobFailures_JobId",
                table: "JobFailures",
                column: "JobId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobStatuses_Jobs_JobId",
                table: "JobStatuses",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobStatuses_Jobs_JobId",
                table: "JobStatuses");

            migrationBuilder.DropTable(
                name: "JobFailures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobStatuses",
                table: "JobStatuses");

            migrationBuilder.RenameTable(
                name: "JobStatuses",
                newName: "JobStatusEntity");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "JobStatusEntity",
                newName: "JobStatus");

            migrationBuilder.RenameIndex(
                name: "IX_JobStatuses_JobId",
                table: "JobStatusEntity",
                newName: "IX_JobStatusEntity_JobId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobStatusEntity",
                table: "JobStatusEntity",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobStatusEntity_Jobs_JobId",
                table: "JobStatusEntity",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
