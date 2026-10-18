// <copyright file="ProtobufChatViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Shows chat messages of nearby players, the party, the guild and whispers (S2CChatMessage).
/// </summary>
internal sealed class ProtobufChatViewPlugIn : IChatViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufChatViewPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ChatMessageAsync(string message, string sender, ChatMessageType type) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ChatMessage = new S2CChatMessage { Sender = sender, Text = message, Type = type.ToString() },
        });
}
