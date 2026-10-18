// <copyright file="20261018000001_AddChargeSettingsToSkill.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddChargeSettingsToSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The defaults are the values the charge mechanic had in code before.
            migrationBuilder.AddColumn<int>(
                name: "ChargeMinimumDamagePercent",
                schema: "config",
                table: "Skill",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "ChargeHoldMilliseconds",
                schema: "config",
                table: "Skill",
                type: "integer",
                nullable: false,
                defaultValue: 1500);

            migrationBuilder.AddColumn<int>(
                name: "ChargeMovementSpeedPercent",
                schema: "config",
                table: "Skill",
                type: "integer",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ChargeMinimumDamagePercent", schema: "config", table: "Skill");
            migrationBuilder.DropColumn(name: "ChargeHoldMilliseconds", schema: "config", table: "Skill");
            migrationBuilder.DropColumn(name: "ChargeMovementSpeedPercent", schema: "config", table: "Skill");
        }
    }
}
