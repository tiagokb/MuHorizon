// <copyright file="ProtobufMarketPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Market;
using MUnique.OpenMU.Network.Protobuf;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The market for the Protobuf client: opening, search results and the own listings.
/// </summary>
internal sealed class ProtobufMarketPlugIn : IMarketViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufMarketPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowMarketOpenAsync(int feePercent, int listingDays) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            MarketOpen = new S2CMarketOpen { FeePercent = (uint)feePercent, ListingDays = (uint)listingDays },
        });

    public ValueTask ShowMarketListingsAsync(IReadOnlyList<MarketListing> listings)
    {
        var msg = new S2CMarketListings();
        msg.Listings.Add(listings.Select(ToData).OfType<MarketListingData>());
        return _player.SendEnvelopeAsync(new ServerEnvelope { MarketListings = msg });
    }

    public ValueTask ShowOwnListingsAsync(IReadOnlyList<MarketListing> listings, int maxListings)
    {
        var msg = new S2CMarketOwn { MaxListings = (uint)maxListings };
        msg.Listings.Add(listings.Select(ToData).OfType<MarketListingData>());
        return _player.SendEnvelopeAsync(new ServerEnvelope { MarketOwn = msg });
    }

    public ValueTask ShowMarketBusyAsync(string? status) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            MarketBusy = new S2CMarketBusy { Busy = status is not null, Status = status ?? string.Empty },
        });

    private static MarketListingData? ToData(MarketListing listing)
    {
        var data = new MarketListingData
        {
            Id = listing.GetId().ToString(),
            Price = (ulong)Math.Max(0, listing.Price),
            Seller = listing.SellerName,
            HoursLeft = (uint)Math.Max(0, Math.Ceiling((listing.ExpiresAt - DateTime.UtcNow).TotalHours)),
            State = (Network.Protobuf.MarketListingState)listing.State,
            Buyer = listing.BuyerName,
        };
        if (listing.Custody?.Items.FirstOrDefault() is { } item)
        {
            data.Item = ProtobufUpdateInventoryListPlugIn.BuildItemData(item);
        }

        return data;
    }
}
