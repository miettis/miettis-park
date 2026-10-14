using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MiettisPark.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parking_garages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    parking_fee_configuration = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_garages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parking_lots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parking_garage_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    current_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_lots", x => x.id);
                    table.CheckConstraint("ck_parking_lots_number", "number > 0");
                    table.CheckConstraint("ck_parking_lots_status", "current_status IN ('Free', 'Occupied', 'Unknown')");
                    table.ForeignKey(
                        name: "FK_parking_lots_parking_garages_parking_garage_id",
                        column: x => x.parking_garage_id,
                        principalTable: "parking_garages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parking_garage_id = table.Column<int>(type: "integer", nullable: false),
                    license_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closure_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visits", x => x.id);
                    table.CheckConstraint("ck_visits_cost", "cost IS NULL OR cost >= 0");
                    table.CheckConstraint("ck_visits_license", "length(license_number) BETWEEN 1 AND 20");
                    table.CheckConstraint("ck_visits_lifecycle", "(status = 'Active' AND end_at IS NULL AND closed_at IS NULL AND cost IS NULL AND closure_reason IS NULL)\nOR (status = 'Completed' AND end_at IS NOT NULL AND closed_at IS NOT NULL AND closed_at = end_at AND cost IS NOT NULL)\nOR (status = 'Interrupted' AND end_at IS NULL AND closed_at IS NOT NULL AND cost IS NULL AND closure_reason IS NOT NULL)");
                    table.CheckConstraint("ck_visits_times", "(end_at IS NULL OR end_at >= start_at) AND (closed_at IS NULL OR closed_at >= start_at)");
                    table.ForeignKey(
                        name: "FK_visits_parking_garages_parking_garage_id",
                        column: x => x.parking_garage_id,
                        principalTable: "parking_garages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parkings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    visit_id = table.Column<int>(type: "integer", nullable: true),
                    parking_lot_id = table.Column<int>(type: "integer", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parkings", x => x.id);
                    table.CheckConstraint("ck_parkings_lifecycle", "(status = 'Active' AND end_at IS NULL AND closed_at IS NULL)\nOR (status = 'Completed' AND end_at IS NOT NULL AND closed_at IS NOT NULL AND closed_at = end_at)\nOR (status = 'Interrupted' AND end_at IS NULL AND closed_at IS NOT NULL)");
                    table.CheckConstraint("ck_parkings_times", "(end_at IS NULL OR end_at >= start_at) AND (closed_at IS NULL OR closed_at >= start_at)");
                    table.ForeignKey(
                        name: "FK_parkings_parking_lots_parking_lot_id",
                        column: x => x.parking_lot_id,
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parkings_visits_visit_id",
                        column: x => x.visit_id,
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "parking_garages",
                columns: new[] { "id", "name", "parking_fee_configuration" },
                values: new object[] { 1, "Keskustan pysäköinti", "[{\"amount\":0.50,\"perUnit\":\"00:10:00\",\"startDuration\":\"00:00:00\",\"endDuration\":\"03:00:00\"},{\"amount\":0.30,\"perUnit\":\"00:10:00\",\"startDuration\":\"03:00:00\",\"endDuration\":null}]" });

            migrationBuilder.InsertData(
                table: "parking_lots",
                columns: new[] { "id", "current_status", "number", "parking_garage_id" },
                values: new object[,]
                {
                    { 1, "Free", 1, 1 },
                    { 2, "Free", 2, 1 },
                    { 3, "Free", 3, 1 },
                    { 4, "Free", 4, 1 },
                    { 5, "Free", 5, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_parking_lots_parking_garage_id_number",
                table: "parking_lots",
                columns: new[] { "parking_garage_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parkings_parking_lot_id",
                table: "parkings",
                column: "parking_lot_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_parkings_visit_id",
                table: "parkings",
                column: "visit_id",
                unique: true,
                filter: "status = 'Active' AND visit_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_visits_parking_garage_id_license_number",
                table: "visits",
                columns: new[] { "parking_garage_id", "license_number" },
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parkings");

            migrationBuilder.DropTable(
                name: "parking_lots");

            migrationBuilder.DropTable(
                name: "visits");

            migrationBuilder.DropTable(
                name: "parking_garages");
        }
    }
}
