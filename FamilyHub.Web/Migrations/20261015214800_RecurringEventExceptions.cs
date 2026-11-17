using FamilyHub.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyHub.Web.Migrations
{
    [DbContext(typeof(FamilyHubDbContext))]
    [Migration("20261015214800_RecurringEventExceptions")]
    public partial class RecurringEventExceptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceOccurrenceDate",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecurrenceIsDeleted",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecurrenceIsOverride",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE Events
                SET RecurrenceOccurrenceDate = datetime(Start, 'start of day')
                WHERE RecurrenceSeriesId IS NOT NULL
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RecurrenceOccurrenceDate", table: "Events");
            migrationBuilder.DropColumn(name: "RecurrenceIsDeleted", table: "Events");
            migrationBuilder.DropColumn(name: "RecurrenceIsOverride", table: "Events");
        }
    }
}
