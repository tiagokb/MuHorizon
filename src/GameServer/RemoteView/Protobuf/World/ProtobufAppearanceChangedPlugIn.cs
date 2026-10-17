// <copyright file="ProtobufAppearanceChangedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

// Sends the full visible-equipment snapshot instead of the single changed item:
// the client replaces its state wholesale, so a lost or reordered message can't leave
// a stale weapon or armor piece on screen.
// GameLogic only notifies observers (not the player itself); the own client renders
// its appearance from S2CInventory / S2CItemAppeared / S2CItemRemoved.
internal sealed class ProtobufAppearanceChangedPlugIn : IAppearanceChangedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufAppearanceChangedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask AppearanceChangedAsync(Player changedPlayer, Item changedItem, bool isEquipped)
    {
        var msg = new S2CAppearanceChanged { EntityId = changedPlayer.Id };
        msg.Equipment.AddRange(ProtobufEquipment.Build(changedPlayer));
        return _player.SendEnvelopeAsync(new ServerEnvelope { AppearanceChanged = msg });
    }
}
