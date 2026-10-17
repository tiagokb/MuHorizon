// <copyright file="ProtobufNpcDialogClosedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Tells the client that the NPC window was closed on the server (S2CNpcDialogClosed).
/// </summary>
internal sealed class ProtobufNpcDialogClosedPlugIn : INpcDialogClosedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufNpcDialogClosedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask DialogClosedAsync(MonsterDefinition npc) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { NpcDialogClosed = new S2CNpcDialogClosed() });
}
