// <copyright file="IMarketViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Market;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Shows the market (see <see cref="MarketService"/>).
/// </summary>
public interface IMarketViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Opens the market (the player talked to the market NPC).
    /// </summary>
    /// <param name="feePercent">The listing fee in percent of the price.</param>
    /// <param name="listingDays">How many days a listing lasts.</param>
    ValueTask ShowMarketOpenAsync(int feePercent, int listingDays);

    /// <summary>
    /// Shows search results.
    /// </summary>
    /// <param name="listings">The listings.</param>
    ValueTask ShowMarketListingsAsync(IReadOnlyList<MarketListing> listings);

    /// <summary>
    /// Shows the player's own listings.
    /// </summary>
    /// <param name="listings">The own listings (on sale, sold, returned).</param>
    /// <param name="maxListings">How many can be on sale at once (per account).</param>
    ValueTask ShowOwnListingsAsync(IReadOnlyList<MarketListing> listings, int maxListings);

    /// <summary>
    /// Shows that the market is working on the player's request (the player is locked meanwhile).
    /// </summary>
    /// <param name="status">What it's doing, e.g. "Publishing…"; <c>null</c> when done.</param>
    ValueTask ShowMarketBusyAsync(string? status);
}
