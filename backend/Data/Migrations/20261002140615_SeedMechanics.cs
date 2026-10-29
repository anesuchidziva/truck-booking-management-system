using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Amcotts.Workshop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedMechanics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Mechanics",
                columns: new[] { "Id", "DisplayName", "IsActive", "IsAvailable", "JobTitle", "WorkloadCapacity" },
                values: new object[,]
                {
                    { 1, "Marcus Chen", true, true, "Senior Technician", 4 },
                    { 2, "Sarah Jenkins", true, true, "Diagnostic Specialist", 4 },
                    { 3, "David Smith", true, true, "General Mechanic", 5 }
                });

            migrationBuilder.InsertData(
                table: "MechanicSkills",
                columns: new[] { "Id", "FaultCategory", "MechanicId" },
                values: new object[,]
                {
                    { 1, "Brake system pressure fault", 1 },
                    { 2, "Electrical fault", 1 },
                    { 3, "Electrical fault", 2 },
                    { 4, "Engine performance fault", 2 },
                    { 5, "Brake system pressure fault", 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MechanicSkills",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MechanicSkills",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MechanicSkills",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "MechanicSkills",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MechanicSkills",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Mechanics",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Mechanics",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Mechanics",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
