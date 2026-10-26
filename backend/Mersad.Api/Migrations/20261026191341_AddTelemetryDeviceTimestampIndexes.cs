using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mersad.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryDeviceTimestampIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessEvents_DeviceId",
                table: "ProcessEvents");

            migrationBuilder.DropIndex(
                name: "IX_NetworkEvents_DeviceId",
                table: "NetworkEvents");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_DeviceId_Timestamp",
                table: "ProcessEvents",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEvents_DeviceId_Timestamp",
                table: "NetworkEvents",
                columns: new[] { "DeviceId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessEvents_DeviceId_Timestamp",
                table: "ProcessEvents");

            migrationBuilder.DropIndex(
                name: "IX_NetworkEvents_DeviceId_Timestamp",
                table: "NetworkEvents");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_DeviceId",
                table: "ProcessEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEvents_DeviceId",
                table: "NetworkEvents",
                column: "DeviceId");
        }
    }
}
