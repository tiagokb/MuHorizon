// <copyright file="MarketListing.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

using MUnique.OpenMU.Annotations;

/// <summary>
/// The state of a <see cref="MarketListing"/>.
/// </summary>
public enum MarketListingState
{
    /// <summary>
    /// On sale.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Sold: the seller can claim the zen.
    /// </summary>
    Sold = 1,

    /// <summary>
    /// Expired or cancelled: the seller can claim the item back.
    /// </summary>
    Returned = 2,
}

/// <summary>
/// An item on the market: it's kept in <see cref="Custody"/> until it's bought (then the seller
/// claims the zen) or it expires / is cancelled (then the seller claims the item back).
/// </summary>
public class MarketListing
{
    /// <summary>
    /// Gets or sets the id of the seller's account (listings are limited per account).
    /// </summary>
    public Guid SellerAccountId { get; set; }

    /// <summary>
    /// Gets or sets the name of the character who listed it.
    /// </summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage which keeps the item while it's listed.
    /// </summary>
    [MemberOfAggregate]
    public virtual ItemStorage? Custody { get; set; }

    /// <summary>
    /// Gets or sets the price in zen.
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Gets or sets when it was listed.
    /// </summary>
    public DateTime ListedAt { get; set; }

    /// <summary>
    /// Gets or sets when it expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public MarketListingState State { get; set; }

    /// <summary>
    /// Gets or sets who bought it.
    /// </summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item name, for the search.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item group (weapons, armors, jewels...), for the search.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the item level, for the search.
    /// </summary>
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is excellent, for the search.
    /// </summary>
    public bool IsExcellent { get; set; }
}
