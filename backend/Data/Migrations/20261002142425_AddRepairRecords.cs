using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amcotts.Workshop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepairRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    MechanicId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedPrimaryFault = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkPerformed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartsUsed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepairStartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepairCompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairRecords_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepairRecords_BookingId",
                table: "RepairRecords",
                column: "BookingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairRecords");
        }
    }
}
