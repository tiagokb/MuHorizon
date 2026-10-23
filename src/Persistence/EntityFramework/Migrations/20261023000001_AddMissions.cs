// <copyright file="20261023000001_AddMissions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <summary>
    /// The missions (new quest system): definitions with steps, objectives and rewards, and the
    /// progress of the characters.
    /// </summary>
    public partial class AddMissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissionDefinition",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    GiverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiverId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinimumLevel = table.Column<int>(type: "integer", nullable: false),
                    MaximumLevel = table.Column<int>(type: "integer", nullable: false),
                    QualifiedCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Repeat = table.Column<int>(type: "integer", nullable: false),
                    PrerequisiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameConfigurationId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionDefinition", x => x.Id);
                    table.ForeignKey("FK_MissionDefinition_GameConfiguration_GameConfigurationId", x => x.GameConfigurationId, principalSchema: "config", principalTable: "GameConfiguration", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_MissionDefinition_MonsterDefinition_GiverId", x => x.GiverId, principalSchema: "config", principalTable: "MonsterDefinition", principalColumn: "Id");
                    table.ForeignKey("FK_MissionDefinition_MonsterDefinition_ReceiverId", x => x.ReceiverId, principalSchema: "config", principalTable: "MonsterDefinition", principalColumn: "Id");
                    table.ForeignKey("FK_MissionDefinition_CharacterClass_QualifiedCharacterId", x => x.QualifiedCharacterId, principalSchema: "config", principalTable: "CharacterClass", principalColumn: "Id");
                    table.ForeignKey("FK_MissionDefinition_MissionDefinition_PrerequisiteId", x => x.PrerequisiteId, principalSchema: "config", principalTable: "MissionDefinition", principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MissionStep",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MissionDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionStep", x => x.Id);
                    table.ForeignKey("FK_MissionStep_MissionDefinition_MissionDefinitionId", x => x.MissionDefinitionId, principalSchema: "config", principalTable: "MissionDefinition", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionObjective",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MonsterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MapId = table.Column<Guid>(type: "uuid", nullable: true),
                    X = table.Column<byte>(type: "smallint", nullable: false),
                    Y = table.Column<byte>(type: "smallint", nullable: false),
                    Radius = table.Column<byte>(type: "smallint", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    MissionStepId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionObjective", x => x.Id);
                    table.ForeignKey("FK_MissionObjective_MissionStep_MissionStepId", x => x.MissionStepId, principalSchema: "config", principalTable: "MissionStep", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_MissionObjective_MonsterDefinition_MonsterId", x => x.MonsterId, principalSchema: "config", principalTable: "MonsterDefinition", principalColumn: "Id");
                    table.ForeignKey("FK_MissionObjective_ItemDefinition_ItemId", x => x.ItemId, principalSchema: "config", principalTable: "ItemDefinition", principalColumn: "Id");
                    table.ForeignKey("FK_MissionObjective_GameMapDefinition_MapId", x => x.MapId, principalSchema: "config", principalTable: "GameMapDefinition", principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MissionReward",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    MissionDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionReward", x => x.Id);
                    table.ForeignKey("FK_MissionReward_MissionDefinition_MissionDefinitionId", x => x.MissionDefinitionId, principalSchema: "config", principalTable: "MissionDefinition", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_MissionReward_ItemDefinition_ItemId", x => x.ItemId, principalSchema: "config", principalTable: "ItemDefinition", principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CharacterMission",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsTracked = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterMission", x => x.Id);
                    table.ForeignKey("FK_CharacterMission_Character_CharacterId", x => x.CharacterId, principalSchema: "data", principalTable: "Character", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_CharacterMission_MissionDefinition_MissionId", x => x.MissionId, principalSchema: "config", principalTable: "MissionDefinition", principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(name: "IX_MissionDefinition_GameConfigurationId", schema: "config", table: "MissionDefinition", column: "GameConfigurationId");
            migrationBuilder.CreateIndex(name: "IX_MissionDefinition_GiverId", schema: "config", table: "MissionDefinition", column: "GiverId");
            migrationBuilder.CreateIndex(name: "IX_MissionDefinition_ReceiverId", schema: "config", table: "MissionDefinition", column: "ReceiverId");
            migrationBuilder.CreateIndex(name: "IX_MissionDefinition_QualifiedCharacterId", schema: "config", table: "MissionDefinition", column: "QualifiedCharacterId");
            migrationBuilder.CreateIndex(name: "IX_MissionDefinition_PrerequisiteId", schema: "config", table: "MissionDefinition", column: "PrerequisiteId");
            migrationBuilder.CreateIndex(name: "IX_MissionStep_MissionDefinitionId", schema: "config", table: "MissionStep", column: "MissionDefinitionId");
            migrationBuilder.CreateIndex(name: "IX_MissionObjective_MissionStepId", schema: "config", table: "MissionObjective", column: "MissionStepId");
            migrationBuilder.CreateIndex(name: "IX_MissionObjective_MonsterId", schema: "config", table: "MissionObjective", column: "MonsterId");
            migrationBuilder.CreateIndex(name: "IX_MissionObjective_ItemId", schema: "config", table: "MissionObjective", column: "ItemId");
            migrationBuilder.CreateIndex(name: "IX_MissionObjective_MapId", schema: "config", table: "MissionObjective", column: "MapId");
            migrationBuilder.CreateIndex(name: "IX_MissionReward_MissionDefinitionId", schema: "config", table: "MissionReward", column: "MissionDefinitionId");
            migrationBuilder.CreateIndex(name: "IX_MissionReward_ItemId", schema: "config", table: "MissionReward", column: "ItemId");
            migrationBuilder.CreateIndex(name: "IX_CharacterMission_CharacterId", schema: "data", table: "CharacterMission", column: "CharacterId");
            migrationBuilder.CreateIndex(name: "IX_CharacterMission_MissionId", schema: "data", table: "CharacterMission", column: "MissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CharacterMission", schema: "data");
            migrationBuilder.DropTable(name: "MissionReward", schema: "config");
            migrationBuilder.DropTable(name: "MissionObjective", schema: "config");
            migrationBuilder.DropTable(name: "MissionStep", schema: "config");
            migrationBuilder.DropTable(name: "MissionDefinition", schema: "config");
        }
    }
}
