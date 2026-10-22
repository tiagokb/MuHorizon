// <copyright file="20261022000001_AddMountSlot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <summary>
    /// The mount slot (12) is a new equipment slot: the inventory starts one slot later. Moves the
    /// items of the character inventories (not the vaults) one slot on, and equipped mounts from the
    /// pet slot (8) to the mount slot (12).
    /// </summary>
    public partial class AddMountSlot : Migration
    {
        private const string InventoryItems = "FROM data.\"Character\" c WHERE c.\"InventoryId\" = i.\"ItemStorageId\"";

        // Group 13 (0x0d): Horn of Uniria (2), Horn of Dinorant (3), Dark Horse (4), Horn of Fenrir (37 = 0x25).
        private const string Mounts = "('00000080-000d-0002-0000-000000000000', '00000080-000d-0003-0000-000000000000', '00000080-000d-0004-0000-000000000000', '00000080-000d-0025-0000-000000000000')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE data.\"Item\" i SET \"ItemSlot\" = i.\"ItemSlot\" + 1 {InventoryItems} AND i.\"ItemSlot\" >= 12;");
            migrationBuilder.Sql($"UPDATE data.\"Item\" i SET \"ItemSlot\" = 12 {InventoryItems} AND i.\"ItemSlot\" = 8 AND i.\"DefinitionId\" IN {Mounts};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE data.\"Item\" i SET \"ItemSlot\" = 8 {InventoryItems} AND i.\"ItemSlot\" = 12 AND NOT EXISTS (SELECT 1 FROM data.\"Item\" p WHERE p.\"ItemStorageId\" = i.\"ItemStorageId\" AND p.\"ItemSlot\" = 8);");
            migrationBuilder.Sql($"UPDATE data.\"Item\" i SET \"ItemSlot\" = i.\"ItemSlot\" - 1 {InventoryItems} AND i.\"ItemSlot\" >= 13;");
        }
    }
}
