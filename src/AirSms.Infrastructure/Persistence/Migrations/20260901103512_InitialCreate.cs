using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirSms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FlightNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AircraftRegistration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_AssignedToUserId",
                table: "incidents",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_Category",
                table: "incidents",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_ReportedAt",
                table: "incidents",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_Severity",
                table: "incidents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_Status",
                table: "incidents",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidents");
        }
    }
}
