// <copyright file="IMarketStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The market's writes, each one database transaction: an item only ever changes its storage (it's
/// never copied), together with the zen and the listing, and only if everything is still as
/// expected — otherwise nothing changes. Every write is logged (data."MarketLog").
/// </summary>
public interface IMarketStore
{
    /// <summary>
    /// Lists an item: it moves from the seller's inventory into a new custody storage, the fee is
    /// taken from the inventory's zen.
    /// </summary>
    /// <param name="listing">The listing values (seller, price, dates, search fields); its id is used.</param>
    /// <param name="listingId">The id of the new listing.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="inventoryId">The seller's inventory storage (where the item is now).</param>
    /// <param name="fee">The fee in zen.</param>
    /// <returns><c>true</c> if it was done.</returns>
    ValueTask<bool> ListAsync(MarketListing listing, Guid listingId, Guid itemId, Guid inventoryId, int fee);

    /// <summary>
    /// Buys an active listing: the price leaves the buyer's inventory zen, the item moves into the
    /// buyer's inventory at the slot, the listing becomes sold.
    /// </summary>
    /// <param name="listingId">The listing.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="buyerName">The buyer's name.</param>
    /// <param name="buyerAccountId">The buyer's account.</param>
    /// <param name="buyerInventoryId">The buyer's inventory storage.</param>
    /// <param name="slot">The free inventory slot.</param>
    /// <returns><c>true</c> if it was done.</returns>
    ValueTask<bool> BuyAsync(Guid listingId, Guid itemId, string buyerName, Guid buyerAccountId, Guid buyerInventoryId, byte slot);

    /// <summary>
    /// Takes an own active listing off the market: its item can be claimed back.
    /// </summary>
    /// <param name="listingId">The listing.</param>
    /// <param name="accountId">The seller's account.</param>
    /// <param name="characterName">Who does it (for the log).</param>
    /// <returns><c>true</c> if it was done.</returns>
    ValueTask<bool> CancelAsync(Guid listingId, Guid accountId, string characterName);

    /// <summary>
    /// Claims the item of an own returned listing into the inventory at the slot; the listing is removed.
    /// </summary>
    /// <param name="listingId">The listing.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="accountId">The seller's account.</param>
    /// <param name="characterName">Who claims it.</param>
    /// <param name="inventoryId">The inventory storage.</param>
    /// <param name="slot">The free inventory slot.</param>
    /// <returns><c>true</c> if it was done.</returns>
    ValueTask<bool> ClaimItemAsync(Guid listingId, Guid itemId, Guid accountId, string characterName, Guid inventoryId, byte slot);

    /// <summary>
    /// Claims the zen of an own sold listing into the inventory's zen; the listing is removed.
    /// </summary>
    /// <param name="listingId">The listing.</param>
    /// <param name="accountId">The seller's account.</param>
    /// <param name="characterName">Who claims it.</param>
    /// <param name="inventoryId">The inventory storage.</param>
    /// <param name="maximumMoney">The most zen the inventory may hold.</param>
    /// <returns>The claimed zen, or <c>null</c> if nothing was done.</returns>
    ValueTask<long?> ClaimMoneyAsync(Guid listingId, Guid accountId, string characterName, Guid inventoryId, int maximumMoney);

    /// <summary>
    /// Returns the expired active listings to their sellers.
    /// </summary>
    /// <returns>How many expired.</returns>
    ValueTask<int> ExpireAsync();
}
