// <copyright file="ProtobufShowCharacterCreationFailedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;

using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CCreateCharacterResult"/> (failure) via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufShowCharacterCreationFailedPlugIn : IShowCharacterCreationFailedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufShowCharacterCreationFailedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ProtobufShowCharacterCreationFailedPlugIn(ProtobufRemotePlayer player) => _player = player;

    /// <inheritdoc />
    public ValueTask ShowCharacterCreationFailedAsync()
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            CreateCharacterResult = new S2CCreateCharacterResult { Success = false },
        });
}
