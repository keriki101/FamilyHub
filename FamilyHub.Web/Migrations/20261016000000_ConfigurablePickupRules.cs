using FamilyHub.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyHub.Web.Migrations
{
    [DbContext(typeof(FamilyHubDbContext))]
    [Migration("20261016000000_ConfigurablePickupRules")]
    public partial class ConfigurablePickupRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWorkEvent",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE Events
                SET IsWorkEvent = 1
                WHERE lower(trim(Title)) = 'jobb'
                """);

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PickupPersonOneId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PickupPersonTwoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_Settings", x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Settings");
            migrationBuilder.DropColumn(name: "IsWorkEvent", table: "Events");
        }
    }
}
