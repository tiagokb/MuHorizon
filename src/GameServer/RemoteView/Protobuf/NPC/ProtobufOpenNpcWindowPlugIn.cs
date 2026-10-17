// <copyright file="ProtobufOpenNpcWindowPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// Opens NPC windows on the client. Only the chaos machine is supported so far (S2CChaosMachine);
/// shops and vaults have their own messages.
/// </summary>
internal sealed class ProtobufOpenNpcWindowPlugIn : IOpenNpcWindowPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufOpenNpcWindowPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask OpenNpcWindowAsync(NpcWindow window) =>
        window == NpcWindow.ChaosMachine ? ProtobufChaosMachine.SendAsync(_player, opened: true) : ValueTask.CompletedTask;
}
