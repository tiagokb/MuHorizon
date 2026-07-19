// <copyright file="ProtobufShowCreatedCharacterPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CCreateCharacterResult"/> (success) via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufShowCreatedCharacterPlugIn : IShowCreatedCharacterPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufShowCreatedCharacterPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ProtobufShowCreatedCharacterPlugIn(ProtobufRemotePlayer player) => _player = player;

    /// <inheritdoc />
    public ValueTask ShowCreatedCharacterAsync(DataModel.Entities.Character character)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            CreateCharacterResult = new S2CCreateCharacterResult
            {
                Success = true,
                Character = BuildInfo(character),
            },
        });

    internal static CharacterInfo BuildInfo(DataModel.Entities.Character c)
        => new CharacterInfo
        {
            Slot = c.CharacterSlot,
            Name = c.Name,
            Level = (uint)(c.Attributes.FirstOrDefault(a => a.Definition == Stats.Level)?.Value ?? 1),
            ClassNumber = (uint)(c.CharacterClass?.Number ?? 0),
            ClassName = c.CharacterClass?.Name ?? string.Empty,
        };
}
