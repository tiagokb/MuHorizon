// <copyright file="ItemCraftAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Items;

using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// The action to craft items with crafting NPCs.
/// </summary>
public class ItemCraftAction
{
    // Shared by all players (previews run on every item move), so it must be thread-safe.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ItemCrafting, IItemCraftingHandler> _craftingHandlerCache = new();

    /// <summary>
    /// Mixes the items at the currently opened crafting NPC.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="mixTypeId">The mix type identifier.</param>
    /// <param name="socketSlot">The socket slot.</param>
    public async ValueTask MixItemsAsync(Player player, byte mixTypeId, byte socketSlot)
    {
        var npcStats = player.OpenedNpc?.Definition;

        var crafting = npcStats?.ItemCraftings.FirstOrDefault(c => c.Number == mixTypeId);
        if (crafting is null)
        {
            await player.InvokeViewPlugInAsync<IShowItemCraftingResultPlugIn>(p => p.ShowResultAsync(CraftingResult.IncorrectMixItems, null)).ConfigureAwait(false);
            return;
        }

        var craftingHandler = this.GetCraftingHandler(crafting);
        (CraftingResult, Item?) result;
        try
        {
            result = await craftingHandler.DoMixAsync(player, socketSlot).ConfigureAwait(false);
        }
        catch
        {
            result = (CraftingResult.LackingMixItems, null);
        }

        var itemList = player.TemporaryStorage?.Items.ToList() ?? new List<Item>();
        await player.InvokeViewPlugInAsync<IShowItemCraftingResultPlugIn>(p => p.ShowResultAsync(result.Item1, itemList.Count > 1 ? null : result.Item2)).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowMerchantStoreItemListPlugIn>(
            p => p.ShowMerchantStoreItemListAsync(
                itemList,
                npcStats!.NpcWindow == NpcWindow.PetTrainer && result.Item1 != CraftingResult.Success ? StoreKind.ResurrectionFailed : StoreKind.ChaosMachine))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the relevant <see cref="ItemCrafting"/> by testing the mix items against every crafting's item requirements.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The relevant <see cref="ItemCrafting"/>.</returns>
    public ItemCrafting? FindAppropriateCraftingByItems(Player player) => this.GetMixPreview(player)?.Crafting;

    /// <summary>
    /// Finds the crafting the items in the <see cref="Player.TemporaryStorage"/> fit, with its success rate
    /// and price, so the client can show what a mix would do (the legacy client worked this out itself).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The crafting, success rate and price; <c>null</c>, if the items fit no crafting of the opened NPC.</returns>
    public (ItemCrafting Crafting, byte SuccessRate, int Price)? GetMixPreview(Player player)
    {
        if (player.OpenedNpc?.Definition is not { } npc)
        {
            return null;
        }

        // Wing crafting is similar but has one extra requirement (chaos weapon) than
        // chaos weapon crafting, so we set a descending order so it gets get checked first
        foreach (var itemCrafting in npc.ItemCraftings.OrderByDescending(c => c.Number))
        {
            var craftingHandler = this.GetCraftingHandler(itemCrafting);
            if (craftingHandler is BaseItemCraftingHandler baseHandler)
            {
                if (baseHandler.TryGetPreview(player, out var successRate, out var price) is null)
                {
                    return (itemCrafting, successRate, price);
                }
            }
            else if (craftingHandler.TryGetRequiredItems(player, out _, out var successRate) is null)
            {
                return (itemCrafting, successRate, 0);
            }
        }

        return null;
    }

    private IItemCraftingHandler GetCraftingHandler(ItemCrafting crafting) =>
        this._craftingHandlerCache.GetOrAdd(crafting, this.CreateCraftingHandler);

    private IItemCraftingHandler CreateCraftingHandler(ItemCrafting crafting)
    {
        if (!string.IsNullOrWhiteSpace(crafting.ItemCraftingHandlerClassName))
        {
            var type = Type.GetType(crafting.ItemCraftingHandlerClassName);
            if (type != null)
            {
                if (type.BaseType == typeof(SimpleItemCraftingHandler))
                {
                    return (IItemCraftingHandler)Activator.CreateInstance(type, crafting.SimpleCraftingSettings)!;
                }

                return (IItemCraftingHandler)Activator.CreateInstance(type)!;
            }

            throw new ArgumentException($"Item crafting handler '{crafting.ItemCraftingHandlerClassName}' not found.", nameof(crafting));
        }

        if (crafting.SimpleCraftingSettings != null)
        {
            return new SimpleItemCraftingHandler(crafting.SimpleCraftingSettings);
        }

        throw new ArgumentException("No simple crafting settings or item crafting handler name specified.", nameof(crafting));
    }
}