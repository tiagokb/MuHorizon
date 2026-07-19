// <copyright file="ProtobufCharacterFocusedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;

using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CSelectCharacterResult"/> in response to a character being selected/focused,
/// via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufCharacterFocusedPlugIn : ICharacterFocusedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufCharacterFocusedPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ProtobufCharacterFocusedPlugIn(ProtobufRemotePlayer player) => _player = player;

    /// <inheritdoc />
    public ValueTask CharacterFocusedAsync(DataModel.Entities.Character character)
    {
        var result = new S2CSelectCharacterResult { Success = character is not null };
        if (character is not null)
        {
            result.Character = ProtobufShowCreatedCharacterPlugIn.BuildInfo(character);
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { SelectCharacterResult = result });
    }
}
