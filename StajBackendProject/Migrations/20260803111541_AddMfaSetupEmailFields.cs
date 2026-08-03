using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StajBackendProject.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaSetupEmailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MfaSetupEmailCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MfaSetupEmailCodeExpiry",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MfaSetupEmailCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaSetupEmailCodeExpiry",
                table: "Users");
        }
    }
}
