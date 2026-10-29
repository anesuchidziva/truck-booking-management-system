using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amcotts.Workshop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosticSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiagnosticSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticSessions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    FaultCategoryId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    ScoreType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedByRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticSuggestions_DiagnosticSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DiagnosticSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticSessions_BookingId_RequestedUtc",
                table: "DiagnosticSessions",
                columns: new[] { "BookingId", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticSuggestions_SessionId_Rank",
                table: "DiagnosticSuggestions",
                columns: new[] { "SessionId", "Rank" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiagnosticSuggestions");

            migrationBuilder.DropTable(
                name: "DiagnosticSessions");
        }
    }
}
