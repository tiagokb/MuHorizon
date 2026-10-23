// <copyright file="20261022000002_AddCompanionLeashRange.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddCompanionLeashRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "CompanionLeashRange",
                schema: "config",
                table: "GameConfiguration",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CompanionLeashRange", schema: "config", table: "GameConfiguration");
        }
    }
}
