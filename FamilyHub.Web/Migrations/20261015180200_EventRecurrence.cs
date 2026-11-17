using FamilyHub.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyHub.Web.Migrations
{
    [DbContext(typeof(FamilyHubDbContext))]
    [Migration("20261015180200_EventRecurrence")]
    public partial class EventRecurrence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceDays",
                table: "Events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurrenceSeriesId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceStart",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceUntil",
                table: "Events",
                type: "TEXT",
                nullable: true);

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RecurrenceDays", table: "Events");
            migrationBuilder.DropColumn(name: "RecurrenceSeriesId", table: "Events");
            migrationBuilder.DropColumn(name: "RecurrenceStart", table: "Events");
            migrationBuilder.DropColumn(name: "RecurrenceUntil", table: "Events");
        }
    }
}
