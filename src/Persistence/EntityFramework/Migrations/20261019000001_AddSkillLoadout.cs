// <copyright file="20261019000001_AddSkillLoadout.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddSkillLoadout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlotType",
                schema: "config",
                table: "Skill",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "SkillLoadout",
                schema: "data",
                table: "Character",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CombatLockSeconds",
                schema: "config",
                table: "GameConfiguration",
                type: "integer",
                nullable: false,
                defaultValue: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SlotType", schema: "config", table: "Skill");
            migrationBuilder.DropColumn(name: "SkillLoadout", schema: "data", table: "Character");
            migrationBuilder.DropColumn(name: "CombatLockSeconds", schema: "config", table: "GameConfiguration");
        }
    }
}
