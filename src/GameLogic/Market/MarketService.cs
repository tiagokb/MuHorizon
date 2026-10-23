// <copyright file="MarketService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Market;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;
using Nito.AsyncEx;

/// <summary>
/// The market search filter.
/// </summary>
/// <param name="Name">Part of the item name; empty for any.</param>
/// <param name="Category">The item category; <see cref="MarketCategory.Any"/> for any.</param>
/// <param name="MinimumLevel">The minimum item level.</param>
/// <param name="ExcellentOnly">Only excellent items.</param>
/// <param name="Descending">Highest price first.</param>
public readonly record struct MarketFilter(string Name, MarketCategory Category, byte MinimumLevel, bool ExcellentOnly, bool Descending);

/// <summary>
/// The item categories of the market search (by item group).
/// </summary>
public enum MarketCategory
{
    /// <summary>
    /// Any item.
    /// </summary>
    Any,

    /// <summary>
    /// Weapons (swords, axes, maces, spears, bows, staffs: groups 0-5).
    /// </summary>
    Weapon,

    /// <summary>
    /// Armor (shields, helms, armors, pants, gloves, boots: groups 6-11).
    /// </summary>
    Armor,

    /// <summary>
    /// Wings, orbs, scrolls of the wings group and pets, rings, pendants (groups 12, 13).
    /// </summary>
    Accessory,

    /// <summary>
    /// Jewels, potions and other consumables (group 14).
    /// </summary>
    Consumable,

    /// <summary>
    /// Skill scrolls (group 15).
    /// </summary>
    Scroll,
}

/// <summary>
/// The market: players list items for a price (paying a fee), others buy them, sellers claim the
/// zen of sold items and the items which expired or were cancelled.
/// </summary>
/// <remarks>
/// Safety first, speed doesn't matter here:
/// - an item is never copied: it's always the same database row, which only changes its storage;
/// - every write is one database transaction with conditions (<see cref="IMarketStore"/>): the item
///   must be exactly where it's expected, the zen must be there — else nothing changes;
/// - writes are processed one at a time (the market lock), and the player is locked meanwhile
///   (<see cref="Player.MarketBusy"/>: no walking, trading, inventory...);
/// - the player's context lets go of the item before the database moves it, and takes it back
///   only after the transaction committed, so memory and database never disagree;
/// - every write is logged (data."MarketLog").
/// </remarks>
public static class MarketService
{
    /// <summary>
    /// How far (tiles) from the market NPC the market can be used.
    /// </summary>
    private const int NpcRange = 6;

    private const int PageSize = 60;

    private static readonly AsyncLock Lock = new();

    // The read model: listings loaded from the database (never written through); reloaded after writes.
    private static List<MarketListing>? _snapshot;

    /// <summary>
    /// The player talked to an NPC: the market NPC opens the market.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    public static async ValueTask OnTalkAsync(Player player, NPC.NonPlayerCharacter npc)
    {
        var configuration = player.GameContext.Configuration;
        if (npc.Definition.Number == configuration.MarketNpcNumber)
        {
            await player.InvokeViewPlugInAsync<IMarketViewPlugIn>(p => p.ShowMarketOpenAsync(configuration.MarketFeePercent, configuration.MarketListingDays)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Searches the active listings.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="filter">The filter.</param>
    public static async ValueTask SearchAsync(Player player, MarketFilter filter)
    {
        if (!await IsAtMarketAsync(player).ConfigureAwait(false))
        {
            return;
        }

        List<MarketListing> page;
        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            var query = (await SnapshotAsync(player).ConfigureAwait(false))
                .Where(m => m.State == MarketListingState.Active
                            && (filter.Name.Length == 0 || m.ItemName.Contains(filter.Name, StringComparison.OrdinalIgnoreCase))
                            && InCategory(m.ItemGroup, filter.Category)
                            && m.ItemLevel >= filter.MinimumLevel
                            && (!filter.ExcellentOnly || m.IsExcellent));
            page = (filter.Descending ? query.OrderByDescending(m => m.Price) : query.OrderBy(m => m.Price)).Take(PageSize).ToList();
        }

        await player.InvokeViewPlugInAsync<IMarketViewPlugIn>(p => p.ShowMarketListingsAsync(page)).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the player's own listings (on sale, sold, returned).
    /// </summary>
    /// <param name="player">The player.</param>
    public static async ValueTask ShowOwnAsync(Player player)
    {
        List<MarketListing> own;
        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            own = Own(player, await SnapshotAsync(player).ConfigureAwait(false));
        }

        await player.InvokeViewPlugInAsync<IMarketViewPlugIn>(p => p.ShowOwnListingsAsync(own, player.GameContext.Configuration.MarketMaxListingsPerAccount)).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists an item of the player's bag for a price; the fee is paid now.
    /// </summary>
    /// <param name="player">The seller.</param>
    /// <param name="slot">The inventory slot of the item.</param>
    /// <param name="price">The price.</param>
    public static async ValueTask ListAsync(Player player, byte slot, long price)
    {
        if (!await IsAtMarketAsync(player).ConfigureAwait(false)
            || player.Account is not { } account || player.SelectedCharacter is not { Inventory: { } inventoryStorage } character
            || Store(player) is not { } store)
        {
            return;
        }

        var configuration = player.GameContext.Configuration;
        if (player.Inventory?.GetItem(slot) is not { Definition: { } definition } item || slot <= DataModel.InventoryConstants.LastEquippableItemSlotIndex)
        {
            await player.ShowBlueMessageAsync("Pick an item from your bag (not equipped).").ConfigureAwait(false);
            return;
        }

        if (definition.IsBoundToCharacter)
        {
            await player.ShowBlueMessageAsync("This item can't be sold.").ConfigureAwait(false);
            return;
        }

        if (price is < 1 or > int.MaxValue)
        {
            await player.ShowBlueMessageAsync("Invalid price.").ConfigureAwait(false);
            return;
        }

        var fee = (int)Math.Max(1, price * configuration.MarketFeePercent / 100);
        if (player.Money < fee)
        {
            await player.ShowBlueMessageAsync($"The listing fee is {fee:N0} zen.").ConfigureAwait(false);
            return;
        }

        await using var busy = await BeginAsync(player, "Publishing…").ConfigureAwait(false);
        if (busy is null)
        {
            return;
        }

        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            if (Own(player, await SnapshotAsync(player).ConfigureAwait(false)).Count(m => m.State == MarketListingState.Active) >= configuration.MarketMaxListingsPerAccount)
            {
                await player.ShowBlueMessageAsync($"Your account can have at most {configuration.MarketMaxListingsPerAccount} items on the market.").ConfigureAwait(false);
                return;
            }

            // Everything of the player in the database first (the item may be new, e.g. just picked up).
            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                await player.ShowBlueMessageAsync("Couldn't save your character, try again.").ConfigureAwait(false);
                return;
            }

            var listing = new MarketListing
            {
                SellerAccountId = account.GetId(),
                SellerName = character.Name,
                Price = price,
                ListedAt = DateTime.UtcNow,
                ItemName = definition.Name.ToString() ?? string.Empty,
                ItemGroup = definition.Group,
                ItemLevel = item.Level,
                IsExcellent = item.IsExcellent(),
            };
            listing.ExpiresAt = listing.ListedAt.AddDays(Math.Max(1, configuration.MarketListingDays));

            // The player's context lets go of the item before the database moves it.
            player.PersistenceContext.Detach(item);
            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
            if (!await store.ListAsync(listing, GuidV7.NewGuid(), item.GetId(), inventoryStorage.GetId(), fee).ConfigureAwait(false))
            {
                // Nothing changed in the database: the item goes back where it was.
                player.PersistenceContext.Attach(item);
                await player.Inventory.AddItemAsync(slot, item).ConfigureAwait(false);
                await player.ShowBlueMessageAsync("The item couldn't be listed; nothing changed.").ConfigureAwait(false);
                return;
            }

            // The database took the fee: memory follows.
            player.Money -= fee;
            _snapshot = null;
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await player.ShowBlueMessageAsync($"{listing.ItemName} listed for {price:N0} zen (fee {fee:N0}). It stays {configuration.MarketListingDays} days.").ConfigureAwait(false);
            await ShowOwnLockedAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Buys a listed item: the zen goes to the market for the seller, the item to the buyer's bag.
    /// </summary>
    /// <param name="player">The buyer.</param>
    /// <param name="listingId">The listing.</param>
    public static async ValueTask BuyAsync(Player player, Guid listingId)
    {
        if (!await IsAtMarketAsync(player).ConfigureAwait(false)
            || player.Account is not { } account || player.SelectedCharacter is not { Inventory: { } inventoryStorage }
            || Store(player) is not { } store)
        {
            return;
        }

        await using var busy = await BeginAsync(player, "Buying…").ConfigureAwait(false);
        if (busy is null)
        {
            return;
        }

        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            if (await LoadListingAsync(player, listingId).ConfigureAwait(false) is not { State: MarketListingState.Active } listing
                || listing.Custody?.Items.FirstOrDefault() is not { } item)
            {
                await player.ShowBlueMessageAsync("This item isn't for sale anymore.").ConfigureAwait(false);
                _snapshot = null;
                return;
            }

            if (listing.SellerAccountId == account.GetId())
            {
                await player.ShowBlueMessageAsync("That's your own item: cancel it in \"My sales\".").ConfigureAwait(false);
                return;
            }

            if (player.Money < listing.Price)
            {
                await player.ShowBlueMessageAsync("Not enough zen.").ConfigureAwait(false);
                return;
            }

            if (player.Inventory?.CheckInvSpace(item) is not { } slot)
            {
                await player.ShowBlueMessageAsync("Your bag is full.").ConfigureAwait(false);
                return;
            }

            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                await player.ShowBlueMessageAsync("Couldn't save your character, try again.").ConfigureAwait(false);
                return;
            }

            if (!await store.BuyAsync(listingId, item.GetId(), player.Name, account.GetId(), inventoryStorage.GetId(), slot).ConfigureAwait(false))
            {
                _snapshot = null;
                await player.ShowBlueMessageAsync("The item couldn't be bought (maybe someone was faster); nothing changed.").ConfigureAwait(false);
                return;
            }

            // The database moved the item and took the zen: memory follows.
            player.Money -= (int)listing.Price;
            await TakeItemAsync(player, item, slot).ConfigureAwait(false);
            _snapshot = null;
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await player.ShowBlueMessageAsync($"You bought {listing.ItemName} for {listing.Price:N0} zen.").ConfigureAwait(false);
            if (player.GameContext.GetPlayerByCharacterName(listing.SellerName) is { } seller)
            {
                await seller.ShowBlueMessageAsync($"Your {listing.ItemName} sold for {listing.Price:N0} zen — claim it at the market.").ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Takes an own item off the market: it can be claimed back.
    /// </summary>
    /// <param name="player">The seller.</param>
    /// <param name="listingId">The listing.</param>
    public static async ValueTask CancelAsync(Player player, Guid listingId)
    {
        if (player.Account is not { } account || Store(player) is not { } store)
        {
            return;
        }

        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await store.CancelAsync(listingId, account.GetId(), player.Name).ConfigureAwait(false))
            {
                await player.ShowBlueMessageAsync("This item isn't on sale anymore.").ConfigureAwait(false);
            }

            _snapshot = null;
            await ShowOwnLockedAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Claims the zen of a sold item or a returned item.
    /// </summary>
    /// <param name="player">The seller.</param>
    /// <param name="listingId">The listing.</param>
    public static async ValueTask ClaimAsync(Player player, Guid listingId)
    {
        if (!await IsAtMarketAsync(player).ConfigureAwait(false)
            || player.Account is not { } account || player.SelectedCharacter is not { Inventory: { } inventoryStorage }
            || Store(player) is not { } store)
        {
            return;
        }

        await using var busy = await BeginAsync(player, "Claiming…").ConfigureAwait(false);
        if (busy is null)
        {
            return;
        }

        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            var listing = await LoadListingAsync(player, listingId).ConfigureAwait(false);
            if (listing is null || listing.SellerAccountId != account.GetId())
            {
                _snapshot = null;
                await ShowOwnLockedAsync(player).ConfigureAwait(false);
                return;
            }

            if (!await player.SaveProgressAsync().ConfigureAwait(false))
            {
                await player.ShowBlueMessageAsync("Couldn't save your character, try again.").ConfigureAwait(false);
                return;
            }

            if (listing.State == MarketListingState.Sold)
            {
                var maximumMoney = player.GameContext.Configuration.MaximumInventoryMoney;
                if (await store.ClaimMoneyAsync(listingId, account.GetId(), player.Name, inventoryStorage.GetId(), maximumMoney).ConfigureAwait(false) is not { } claimed)
                {
                    await player.ShowBlueMessageAsync("The zen couldn't be claimed (can you carry that much?); nothing changed.").ConfigureAwait(false);
                    return;
                }

                player.Money += (int)claimed;
                await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                await player.ShowBlueMessageAsync($"You claimed {claimed:N0} zen.").ConfigureAwait(false);
            }
            else if (listing is { State: MarketListingState.Returned } && listing.Custody?.Items.FirstOrDefault() is { } item)
            {
                if (player.Inventory?.CheckInvSpace(item) is not { } slot)
                {
                    await player.ShowBlueMessageAsync("Your bag is full.").ConfigureAwait(false);
                    return;
                }

                if (!await store.ClaimItemAsync(listingId, item.GetId(), account.GetId(), player.Name, inventoryStorage.GetId(), slot).ConfigureAwait(false))
                {
                    await player.ShowBlueMessageAsync("The item couldn't be claimed; nothing changed.").ConfigureAwait(false);
                    return;
                }

                await TakeItemAsync(player, item, slot).ConfigureAwait(false);
            }

            _snapshot = null;
            await ShowOwnLockedAsync(player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tells the player what's waiting at the market (after entering the game).
    /// </summary>
    /// <param name="player">The player.</param>
    public static async ValueTask NotifyAsync(Player player)
    {
        List<MarketListing> own;
        using (await Lock.LockAsync().ConfigureAwait(false))
        {
            own = Own(player, await SnapshotAsync(player).ConfigureAwait(false));
        }

        var sold = own.Count(m => m.State == MarketListingState.Sold);
        var returned = own.Count(m => m.State == MarketListingState.Returned);
        if (sold + returned > 0)
        {
            await player.ShowBlueMessageAsync($"Market: {sold} sold and {returned} returned item(s) to claim.").ConfigureAwait(false);
        }
    }

    // The database moved the item into the player's inventory: the player's context takes the very
    // same item (loaded from the database just before) into memory.
    private static async ValueTask TakeItemAsync(Player player, Item item, byte slot)
    {
        item.ItemSlot = slot;
        player.PersistenceContext.Attach(item);
        await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
    }

    // Locks the player for a market write (no walking, trading, inventory...); disposing unlocks.
    private static async ValueTask<IAsyncDisposable?> BeginAsync(Player player, string status)
    {
        if (player.MarketBusy)
        {
            await player.ShowBlueMessageAsync("The market is still working on your last request.").ConfigureAwait(false);
            return null;
        }

        player.MarketBusy = true;
        player.ContinuousWalkTarget = null;
        await player.InvokeViewPlugInAsync<IMarketViewPlugIn>(p => p.ShowMarketBusyAsync(status)).ConfigureAwait(false);
        return new BusyScope(player);
    }

    private static IMarketStore? Store(Player player) => player.GameContext.PersistenceContextProvider.MarketStore;

    // A listing fresh from the database (with its item), in a throw-away context: the objects end
    // up in no context, so the player's context can take the item after a transaction.
    private static async ValueTask<MarketListing?> LoadListingAsync(Player player, Guid listingId)
    {
        using var context = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration);
        return await context.GetByIdAsync<MarketListing>(listingId).ConfigureAwait(false);
    }

    private static async ValueTask<List<MarketListing>> SnapshotAsync(Player player)
    {
        if (_snapshot is null)
        {
            if (Store(player) is { } store)
            {
                await store.ExpireAsync().ConfigureAwait(false);
            }

            using var context = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration);
            _snapshot = (await context.GetAsync<MarketListing>().ConfigureAwait(false)).ToList();
        }

        return _snapshot;
    }

    private static List<MarketListing> Own(Player player, IEnumerable<MarketListing> listings)
    {
        var accountId = player.Account?.GetId();
        return listings.Where(m => m.SellerAccountId == accountId).OrderBy(m => m.State).ThenBy(m => m.ExpiresAt).ToList();
    }

    private static async ValueTask<bool> IsAtMarketAsync(Player player)
    {
        var npcNumber = player.GameContext.Configuration.MarketNpcNumber;
        if (player.LastTalkedNpc is { } npc && npc.Definition.Number == npcNumber
            && npc.CurrentMap == player.CurrentMap && player.IsInRange(npc.Position, NpcRange))
        {
            return true;
        }

        var name = player.GameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == npcNumber)?.Designation.ToString() ?? "the market NPC";
        await player.ShowBlueMessageAsync($"Talk to {name} to use the market.").ConfigureAwait(false);
        return false;
    }

    private static async ValueTask ShowOwnLockedAsync(Player player)
    {
        var own = Own(player, await SnapshotAsync(player).ConfigureAwait(false));
        await player.InvokeViewPlugInAsync<IMarketViewPlugIn>(p => p.ShowOwnListingsAsync(own, player.GameContext.Configuration.MarketMaxListingsPerAccount)).ConfigureAwait(false);
    }

    private static bool InCategory(byte group, MarketCategory category) => category switch
    {
        MarketCategory.Weapon => group <= 5,
        MarketCategory.Armor => group is >= 6 and <= 11,
        MarketCategory.Accessory => group is 12 or 13,
        MarketCategory.Consumable => group == 14,
        MarketCategory.Scroll => group == 15,
        _ => true,
    };

    private sealed class BusyScope(Player player) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            player.MarketBusy = false;
            await player.InvokeViewPlugInAsync<IMarketViewPlugIn>(p => p.ShowMarketBusyAsync(null)).ConfigureAwait(false);
        }
    }
}
