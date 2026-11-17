using FamilyHub.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyHub.Web.Migrations
{
    [DbContext(typeof(FamilyHubDbContext))]
    [Migration("20261015181500_EventRecurrenceSeriesIndex")]
    public partial class EventRecurrenceSeriesIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_RecurrenceSeriesId",
                table: "Events",
                column: "RecurrenceSeriesId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_RecurrenceSeriesId",
                table: "Events");
        }
    }
}
