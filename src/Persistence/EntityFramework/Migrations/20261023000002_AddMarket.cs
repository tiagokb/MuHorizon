// <copyright file="20261023000002_AddMarket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <summary>
    /// The market: listings (with the storage which keeps the item) and its settings.
    /// </summary>
    public partial class AddMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketListing",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "text", nullable: false),
                    CustodyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    ListedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    BuyerName = table.Column<string>(type: "text", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    IsExcellent = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketListing", x => x.Id);
                    table.ForeignKey("FK_MarketListing_ItemStorage_CustodyId", x => x.CustodyId, principalSchema: "data", principalTable: "ItemStorage", principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(name: "IX_MarketListing_CustodyId", schema: "data", table: "MarketListing", column: "CustodyId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_MarketListing_SellerAccountId", schema: "data", table: "MarketListing", column: "SellerAccountId");

            migrationBuilder.AddColumn<int>(name: "MarketFeePercent", schema: "config", table: "GameConfiguration", type: "integer", nullable: false, defaultValue: 5);
            migrationBuilder.AddColumn<int>(name: "MarketListingDays", schema: "config", table: "GameConfiguration", type: "integer", nullable: false, defaultValue: 4);
            migrationBuilder.AddColumn<int>(name: "MarketMaxListingsPerAccount", schema: "config", table: "GameConfiguration", type: "integer", nullable: false, defaultValue: 10);
            migrationBuilder.AddColumn<short>(name: "MarketNpcNumber", schema: "config", table: "GameConfiguration", type: "smallint", nullable: false, defaultValue: (short)547);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MarketListing", schema: "data");
            migrationBuilder.DropColumn(name: "MarketFeePercent", schema: "config", table: "GameConfiguration");
            migrationBuilder.DropColumn(name: "MarketListingDays", schema: "config", table: "GameConfiguration");
            migrationBuilder.DropColumn(name: "MarketMaxListingsPerAccount", schema: "config", table: "GameConfiguration");
            migrationBuilder.DropColumn(name: "MarketNpcNumber", schema: "config", table: "GameConfiguration");
        }
    }
}
