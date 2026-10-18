// <copyright file="ProtobufPvpStateChangedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Shows that a player (or the own one) turned PvP on or off (S2CPvpChanged).
/// </summary>
internal sealed class ProtobufPvpStateChangedPlugIn : IPvpStateChangedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufPvpStateChangedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask PvpStateChangedAsync(Player player) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            PvpChanged = new S2CPvpChanged { EntityId = player.Id, Enabled = player.PvpEnabled },
        });
}
