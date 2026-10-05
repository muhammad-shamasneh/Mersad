using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mersad.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetworkEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<int>(type: "integer", nullable: false),
                    LocalAddress = table.Column<string>(type: "text", nullable: false),
                    LocalPort = table.Column<int>(type: "integer", nullable: false),
                    RemoteAddress = table.Column<string>(type: "text", nullable: false),
                    RemotePort = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEvents_DeviceId",
                table: "NetworkEvents",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkEvents");
        }
    }
}
