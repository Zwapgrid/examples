using Microsoft.EntityFrameworkCore.Migrations;

namespace CredentialPostTest.Data.Migrations
{
    public partial class AddZgConnectionProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ZgConnectionId",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZgPublicKey",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ZgConnectionId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ZgPublicKey",
                table: "AspNetUsers");
        }
    }
}
