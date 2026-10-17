// <copyright file="ProtobufShowMessagePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends server messages (e.g. why a warp was refused) as <see cref="S2CSystemMessage"/>.
/// </summary>
internal sealed class ProtobufShowMessagePlugIn : IShowMessagePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowMessagePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowMessageAsync(string message, MessageType messageType)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SystemMessage = new S2CSystemMessage { Text = message },
        });
    }
}
