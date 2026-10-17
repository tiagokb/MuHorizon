// <copyright file="ProtobufItemUpgradedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// A jewel changed an item (level, options): resends the inventory so the client shows it.
/// </summary>
internal sealed class ProtobufItemUpgradedPlugIn : IItemUpgradedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemUpgradedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemUpgradedAsync(Item item) =>
        new ProtobufUpdateInventoryListPlugIn(_player).UpdateInventoryListAsync();
}
