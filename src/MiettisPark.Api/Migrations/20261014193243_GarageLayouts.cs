using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiettisPark.Api.Migrations
{
    /// <inheritdoc />
    public partial class GarageLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parking_lots_parking_garage_id_number",
                table: "parking_lots");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "parking_lots",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "parking_garages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "parking_garages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "layout_cells",
                table: "parking_garages",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "layout_height",
                table: "parking_garages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "layout_version",
                table: "parking_garages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "layout_width",
                table: "parking_garages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "parking_garages",
                type: "double precision",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM parking_lots GROUP BY parking_garage_id HAVING count(*) > 2500) THEN
                        RAISE EXCEPTION 'Garage layout backfill supports at most 2500 lots per garage';
                    END IF;
                END $$;

                WITH counts AS (
                    SELECT g.id, count(l.id)::int AS n
                    FROM parking_garages g LEFT JOIN parking_lots l ON l.parking_garage_id = g.id
                    GROUP BY g.id
                ), dimensions AS (
                    SELECT id, greatest(1, least(n, 50)) AS width,
                        greatest(1, (n + 49) / 50) AS height FROM counts
                ), ranked AS (
                    SELECT id, parking_garage_id,
                        row_number() OVER (PARTITION BY parking_garage_id ORDER BY number, id) - 1 AS position
                    FROM parking_lots
                )
                UPDATE parking_garages g SET layout_width = d.width, layout_height = d.height,
                    layout_version = 1, layout_cells = (
                        SELECT jsonb_agg(jsonb_build_object('x', pos % d.width, 'y', pos / d.width,
                            'type', CASE WHEN r.id IS NULL THEN 0 ELSE 2 END, 'lotId', r.id) ORDER BY pos)
                        FROM generate_series(0, d.width * d.height - 1) AS pos
                        LEFT JOIN ranked r ON r.parking_garage_id = d.id AND r.position = pos
                    )
                FROM dimensions d WHERE g.id = d.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_parking_lots_parking_garage_id_number",
                table: "parking_lots",
                columns: new[] { "parking_garage_id", "number" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.AddCheckConstraint(
                name: "ck_garages_layout",
                table: "parking_garages",
                sql: "layout_width BETWEEN 1 AND 50 AND layout_height BETWEEN 1 AND 50 AND layout_version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_garages_location",
                table: "parking_garages",
                sql: "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parking_lots_parking_garage_id_number",
                table: "parking_lots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_garages_layout",
                table: "parking_garages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_garages_location",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "parking_lots");

            migrationBuilder.DropColumn(
                name: "address",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "layout_cells",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "layout_height",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "layout_version",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "layout_width",
                table: "parking_garages");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "parking_garages");

            migrationBuilder.CreateIndex(
                name: "IX_parking_lots_parking_garage_id_number",
                table: "parking_lots",
                columns: new[] { "parking_garage_id", "number" },
                unique: true);
        }
    }
}
