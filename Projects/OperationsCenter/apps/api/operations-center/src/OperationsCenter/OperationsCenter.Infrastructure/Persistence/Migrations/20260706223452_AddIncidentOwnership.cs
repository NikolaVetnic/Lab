using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "incidents",
                table: "incidents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "incidents",
                table: "incidents");
        }
    }
}
