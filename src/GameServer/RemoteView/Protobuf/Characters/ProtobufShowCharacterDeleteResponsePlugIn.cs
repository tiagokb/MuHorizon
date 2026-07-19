// <copyright file="ProtobufShowCharacterDeleteResponsePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;

using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CDeleteCharacterResult"/> via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufShowCharacterDeleteResponsePlugIn : IShowCharacterDeleteResponsePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufShowCharacterDeleteResponsePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ProtobufShowCharacterDeleteResponsePlugIn(ProtobufRemotePlayer player) => _player = player;

    /// <inheritdoc />
    public ValueTask ShowCharacterDeleteResponseAsync(CharacterDeleteResult result)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            DeleteCharacterResult = new S2CDeleteCharacterResult
            {
                Success = result == CharacterDeleteResult.Successful,
            },
        });
}
