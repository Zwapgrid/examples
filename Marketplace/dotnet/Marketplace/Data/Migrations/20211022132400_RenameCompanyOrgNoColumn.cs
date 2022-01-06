using Microsoft.EntityFrameworkCore.Migrations;

namespace Marketplace.Data.Migrations
{
    public partial class RenameCompanyOrgNoColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name:"CompanyOrgNo",
                table: "AspNetUsers",
                newName: "CompanyId"
                );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name:"CompanyId",
                table: "AspNetUsers",
                newName: "CompanyOrgNo"
            );
        }
    }
}
