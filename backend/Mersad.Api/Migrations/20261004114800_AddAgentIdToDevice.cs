using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mersad.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentIdToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                table: "Devices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Devices_AgentId",
                table: "Devices",
                column: "AgentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_AgentId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "Devices");
        }
    }
}
