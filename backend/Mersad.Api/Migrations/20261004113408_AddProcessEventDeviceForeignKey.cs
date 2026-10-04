using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mersad.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessEventDeviceForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_DeviceId",
                table: "ProcessEvents",
                column: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessEvents_Devices_DeviceId",
                table: "ProcessEvents",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessEvents_Devices_DeviceId",
                table: "ProcessEvents");

            migrationBuilder.DropIndex(
                name: "IX_ProcessEvents_DeviceId",
                table: "ProcessEvents");
        }
    }
}
