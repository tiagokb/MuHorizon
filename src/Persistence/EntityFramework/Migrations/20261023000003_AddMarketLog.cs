// <copyright file="20261023000003_AddMarketLog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <summary>
    /// The market's audit log: every market write (list, buy, cancel, claims) with who, what and how much.
    /// It's only written by SQL (MarketStore), so it has no entity.
    /// </summary>
    public partial class AddMarketLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketLog",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_MarketLog", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_MarketLog_AccountId", schema: "data", table: "MarketLog", column: "AccountId");
            migrationBuilder.CreateIndex(name: "IX_MarketLog_ItemId", schema: "data", table: "MarketLog", column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MarketLog", schema: "data");
        }
    }
}
