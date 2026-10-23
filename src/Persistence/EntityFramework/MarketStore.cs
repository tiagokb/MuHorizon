// <copyright file="MarketStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The market's writes as single SQL transactions with conditions (see <see cref="IMarketStore"/>):
/// every statement must affect exactly the expected rows, or the whole transaction is rolled back.
/// </summary>
internal class MarketStore : IMarketStore
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketStore"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public MarketStore(ILoggerFactory loggerFactory) => this._logger = loggerFactory.CreateLogger<MarketStore>();

    /// <inheritdoc />
    public ValueTask<bool> ListAsync(MarketListing listing, Guid listingId, Guid itemId, Guid inventoryId, int fee) =>
        this.RunAsync("list", async db =>
        {
            var custodyId = GuidV7.NewGuid();
            await ExpectAsync(db, 1, $@"UPDATE data.""ItemStorage"" SET ""Money"" = ""Money"" - {fee} WHERE ""Id"" = {inventoryId} AND ""Money"" >= {fee}").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"INSERT INTO data.""ItemStorage"" (""Id"", ""Money"") VALUES ({custodyId}, 0)").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"UPDATE data.""Item"" SET ""ItemStorageId"" = {custodyId}, ""ItemSlot"" = 0 WHERE ""Id"" = {itemId} AND ""ItemStorageId"" = {inventoryId}").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"INSERT INTO data.""MarketListing"" (""Id"", ""SellerAccountId"", ""SellerName"", ""CustodyId"", ""Price"", ""ListedAt"", ""ExpiresAt"", ""State"", ""BuyerName"", ""ItemName"", ""ItemGroup"", ""ItemLevel"", ""IsExcellent"")
                VALUES ({listingId}, {listing.SellerAccountId}, {listing.SellerName}, {custodyId}, {listing.Price}, {listing.ListedAt}, {listing.ExpiresAt}, 0, '', {listing.ItemName}, {(short)listing.ItemGroup}, {(short)listing.ItemLevel}, {listing.IsExcellent})").ConfigureAwait(false);
            await LogAsync(db, "list", listing.SellerAccountId, listing.SellerName, listingId, itemId, listing.ItemName, listing.Price).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public ValueTask<bool> BuyAsync(Guid listingId, Guid itemId, string buyerName, Guid buyerAccountId, Guid buyerInventoryId, byte slot) =>
        this.RunAsync("buy", async db =>
        {
            var listing = await db.Set<Model.MarketListing>().AsNoTracking().FirstOrDefaultAsync(l => l.Id == listingId).ConfigureAwait(false)
                          ?? throw new MarketConflictException("no listing");
            await ExpectAsync(db, 1, $@"UPDATE data.""MarketListing"" SET ""State"" = 1, ""BuyerName"" = {buyerName} WHERE ""Id"" = {listingId} AND ""State"" = 0 AND ""SellerAccountId"" <> {buyerAccountId}").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"UPDATE data.""ItemStorage"" SET ""Money"" = ""Money"" - {listing.Price} WHERE ""Id"" = {buyerInventoryId} AND ""Money"" >= {listing.Price}").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"UPDATE data.""Item"" SET ""ItemStorageId"" = {buyerInventoryId}, ""ItemSlot"" = {(short)slot} WHERE ""Id"" = {itemId} AND ""ItemStorageId"" = {listing.CustodyId}").ConfigureAwait(false);
            await LogAsync(db, "buy", buyerAccountId, buyerName, listingId, itemId, listing.ItemName, listing.Price).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public ValueTask<bool> CancelAsync(Guid listingId, Guid accountId, string characterName) =>
        this.RunAsync("cancel", async db =>
        {
            await ExpectAsync(db, 1, $@"UPDATE data.""MarketListing"" SET ""State"" = 2 WHERE ""Id"" = {listingId} AND ""State"" = 0 AND ""SellerAccountId"" = {accountId}").ConfigureAwait(false);
            await LogAsync(db, "cancel", accountId, characterName, listingId, null, null, 0).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public ValueTask<bool> ClaimItemAsync(Guid listingId, Guid itemId, Guid accountId, string characterName, Guid inventoryId, byte slot) =>
        this.RunAsync("claim item", async db =>
        {
            var listing = await db.Set<Model.MarketListing>().AsNoTracking().FirstOrDefaultAsync(l => l.Id == listingId && l.State == MarketListingState.Returned && l.SellerAccountId == accountId).ConfigureAwait(false)
                          ?? throw new MarketConflictException("no returned listing");
            await ExpectAsync(db, 1, $@"UPDATE data.""Item"" SET ""ItemStorageId"" = {inventoryId}, ""ItemSlot"" = {(short)slot} WHERE ""Id"" = {itemId} AND ""ItemStorageId"" = {listing.CustodyId}").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"DELETE FROM data.""MarketListing"" WHERE ""Id"" = {listingId}").ConfigureAwait(false);
            await ExpectAsync(db, 0, $@"SELECT 1 AS ""Value"" FROM data.""Item"" WHERE ""ItemStorageId"" = {listing.CustodyId}", query: true).ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"DELETE FROM data.""ItemStorage"" WHERE ""Id"" = {listing.CustodyId}").ConfigureAwait(false);
            await LogAsync(db, "claim item", accountId, characterName, listingId, itemId, listing.ItemName, 0).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public async ValueTask<long?> ClaimMoneyAsync(Guid listingId, Guid accountId, string characterName, Guid inventoryId, int maximumMoney)
    {
        long? claimed = null;
        var ok = await this.RunAsync("claim zen", async db =>
        {
            var listing = await db.Set<Model.MarketListing>().AsNoTracking().FirstOrDefaultAsync(l => l.Id == listingId && l.State == MarketListingState.Sold && l.SellerAccountId == accountId).ConfigureAwait(false)
                          ?? throw new MarketConflictException("no sold listing");
            await ExpectAsync(db, 1, $@"UPDATE data.""ItemStorage"" SET ""Money"" = ""Money"" + {listing.Price} WHERE ""Id"" = {inventoryId} AND ""Money"" + {listing.Price} <= {maximumMoney}").ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"DELETE FROM data.""MarketListing"" WHERE ""Id"" = {listingId}").ConfigureAwait(false);
            await ExpectAsync(db, 0, $@"SELECT 1 AS ""Value"" FROM data.""Item"" WHERE ""ItemStorageId"" = {listing.CustodyId}", query: true).ConfigureAwait(false);
            await ExpectAsync(db, 1, $@"DELETE FROM data.""ItemStorage"" WHERE ""Id"" = {listing.CustodyId}").ConfigureAwait(false);
            await LogAsync(db, "claim zen", accountId, characterName, listingId, null, listing.ItemName, listing.Price).ConfigureAwait(false);
            claimed = listing.Price;
        }).ConfigureAwait(false);
        return ok ? claimed : null;
    }

    /// <inheritdoc />
    public async ValueTask<int> ExpireAsync()
    {
        await using var db = new EntityDataContext();
        return await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE data.""MarketListing"" SET ""State"" = 2 WHERE ""State"" = 0 AND ""ExpiresAt"" <= {DateTime.UtcNow}").ConfigureAwait(false);
    }

    private static async ValueTask ExpectAsync(EntityDataContext db, int expectedRows, FormattableString sql, bool query = false)
    {
        int rows;
        if (query)
        {
            rows = await db.Database.SqlQuery<int>(sql).CountAsync().ConfigureAwait(false);
        }
        else
        {
            rows = await db.Database.ExecuteSqlInterpolatedAsync(sql).ConfigureAwait(false);
        }

        if (rows != expectedRows)
        {
            throw new MarketConflictException($"expected {expectedRows} row(s), got {rows}: {sql.Format}");
        }
    }

    private static async ValueTask LogAsync(EntityDataContext db, string action, Guid accountId, string characterName, Guid listingId, Guid? itemId, string? itemName, long price)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO data.""MarketLog"" (""Id"", ""At"", ""Action"", ""AccountId"", ""CharacterName"", ""ListingId"", ""ItemId"", ""ItemName"", ""Price"")
            VALUES ({GuidV7.NewGuid()}, {DateTime.UtcNow}, {action}, {accountId}, {characterName}, {listingId}, {itemId}, {itemName ?? string.Empty}, {price})").ConfigureAwait(false);
    }

    private async ValueTask<bool> RunAsync(string operation, Func<EntityDataContext, Task> work)
    {
        await using var db = new EntityDataContext();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable).ConfigureAwait(false);
        try
        {
            await work(db).ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
            return true;
        }
        catch (MarketConflictException ex)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            this._logger.LogWarning("Market {Operation} refused, nothing changed: {Reason}", operation, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            this._logger.LogError(ex, "Market {Operation} failed, nothing changed.", operation);
            return false;
        }
    }

    /// <summary>
    /// A condition of a market transaction didn't hold: the transaction is rolled back.
    /// </summary>
    private sealed class MarketConflictException(string message) : Exception(message);
}
