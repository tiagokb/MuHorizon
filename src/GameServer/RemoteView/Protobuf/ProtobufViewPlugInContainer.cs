// <copyright file="ProtobufViewPlugInContainer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf;

using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Auth;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Items;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Vault;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A simple <see cref="ICustomPlugInContainer{IViewPlugIn}"/> that wires the Protobuf view plugins
/// directly to the player, bypassing the legacy client-version-based selection mechanism.
/// </summary>
internal sealed class ProtobufViewPlugInContainer : ICustomPlugInContainer<IViewPlugIn>
{
    private readonly Dictionary<Type, IViewPlugIn> _plugins;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufViewPlugInContainer"/> class.
    /// </summary>
    /// <param name="player">The Protobuf remote player that owns this container.</param>
    public ProtobufViewPlugInContainer(ProtobufRemotePlayer player)
    {
        var all = new IViewPlugIn[]
        {
            new ProtobufShowLoginResultPlugIn(player),
            new ProtobufShowCharacterListPlugIn(player),
            new ProtobufShowCreatedCharacterPlugIn(player),
            new ProtobufShowCharacterCreationFailedPlugIn(player),
            new ProtobufCharacterFocusedPlugIn(player),
            new ProtobufShowCharacterDeleteResponsePlugIn(player),
            new ProtobufEnteredWorldPlugIn(player),
            new ProtobufNewPlayersInScopePlugIn(player),
            new ProtobufNewNpcsInScopePlugIn(player),
            new ProtobufObjectsOutOfScopePlugIn(player),
            new ProtobufSnapshotPlugIn(player),
            new ProtobufShowAnimationPlugIn(player),
            new ProtobufShowSkillAnimationPlugIn(player),
            new ProtobufShowAreaSkillAnimationPlugIn(player),
            new ProtobufSkillCooldownPlugIn(player),
            new ProtobufShowCastPlugIn(player),
            new ProtobufMapChangePlugIn(player),
            new ProtobufShowMessagePlugIn(player),
            new ProtobufShowHitPlugIn(player),
            new ProtobufActivateMagicEffectPlugIn(player),
            new ProtobufDeactivateMagicEffectPlugIn(player),
            new ProtobufObjectGotKilledPlugIn(player),
            new ProtobufShowDroppedItemsPlugIn(player),
            new ProtobufShowMoneyDropPlugIn(player),
            new ProtobufDroppedItemsDisappearedPlugIn(player),
            new ProtobufItemAppearPlugIn(player),
            new ProtobufItemPickUpFailedPlugIn(player),
            new ProtobufProjectileSpawnedPlugIn(player),
            new ProtobufProjectileHitPlugIn(player),
            new ProtobufProjectileExpiredPlugIn(player),
            new ProtobufUpdateCharacterStatePlugIn(player),
            new ProtobufStatIncreaseResultPlugIn(player),
            new ProtobufUpdateVitalsPlugIn(player),
            new ProtobufAddExperiencePlugIn(player),
            new ProtobufLevelUpPlugIn(player),
            new ProtobufUpdateInventoryListPlugIn(player),
            new ProtobufItemRemovedPlugIn(player),
            new ProtobufUpdateMoneyPlugIn(player),
            new ProtobufItemMovedPlugIn(player),
            new ProtobufItemMoveFailedPlugIn(player),
            new ProtobufNpcItemBoughtPlugIn(player),
            new ProtobufBuyNpcItemFailedPlugIn(player),
            new ProtobufShowMerchantStoreItemListPlugIn(player),
            new ProtobufOpenNpcWindowPlugIn(player),
            new ProtobufSkillListViewPlugIn(player),
            new ProtobufShowVaultPlugIn(player),
            new ProtobufCloseVaultPlugIn(player),
            new ProtobufItemSoldToNpcPlugIn(player),
            new ProtobufItemSellConfirmRequiredPlugIn(player),
            new ProtobufItemDurabilityChangedPlugIn(player),
            new ProtobufItemDropResultPlugIn(player),
            new ProtobufTeleportPlugIn(player),
            new ProtobufSafezonePlugIn(player),
            new ProtobufAppearanceChangedPlugIn(player),
        };

        _plugins = new Dictionary<Type, IViewPlugIn>();
        foreach (var plugin in all)
        {
            foreach (var iface in plugin.GetType().GetInterfaces())
            {
                if (iface != typeof(IViewPlugIn) && typeof(IViewPlugIn).IsAssignableFrom(iface))
                {
                    _plugins[iface] = plugin;
                }
            }
        }
    }

    /// <inheritdoc />
    public T? GetPlugIn<T>()
        where T : class, IViewPlugIn
        => _plugins.TryGetValue(typeof(T), out var p) ? (T)p : null;
}
