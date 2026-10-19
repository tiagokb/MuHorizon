// <copyright file="ProtobufOpenNpcWindowPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Opens NPC windows on the client. Only the chaos machine is supported so far (S2CChaosMachine);
/// shops and vaults have their own messages.
/// </summary>
internal sealed class ProtobufOpenNpcWindowPlugIn : IOpenNpcWindowPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufOpenNpcWindowPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask OpenNpcWindowAsync(NpcWindow window) => window switch
    {
        NpcWindow.ChaosMachine => ProtobufChaosMachine.SendAsync(_player, opened: true),
        NpcWindow.DevilSquare => this.ShowEntranceAsync(MiniGameKind.DevilSquare),
        NpcWindow.BloodCastle => this.ShowEntranceAsync(MiniGameKind.BloodCastle),
        _ => ValueTask.CompletedTask,
    };

    // Charon / Messenger of Arch: the client shows the event entrance.
    private ValueTask ShowEntranceAsync(MiniGameKind kind) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { MiniGameEntrance = new S2CMiniGameEntrance { Kind = kind } });
}
