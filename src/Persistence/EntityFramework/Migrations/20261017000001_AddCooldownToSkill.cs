// <copyright file="20261017000001_AddCooldownToSkill.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddCooldownToSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CooldownMilliseconds",
                schema: "config",
                table: "Skill",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CooldownMilliseconds",
                schema: "config",
                table: "Skill");
        }
    }
}
