using Backend.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Backend.Api.Infrastructure.Persistence;

#nullable disable

namespace Backend.Api.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PulseDbContext))]
[Migration("202603180001_InitialCreate")]
public partial class _0001_InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "pulse_records",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Bpm = table.Column<int>(type: "integer", nullable: false),
                Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pulse_records", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_pulse_records_ObservedAtUtc",
            table: "pulse_records",
            column: "ObservedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "pulse_records");
    }
}
