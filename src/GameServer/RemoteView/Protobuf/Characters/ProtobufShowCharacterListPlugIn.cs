// <copyright file="ProtobufShowCharacterListPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CCharacterList"/> to the client via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufShowCharacterListPlugIn : IShowCharacterListPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufShowCharacterListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ProtobufShowCharacterListPlugIn(ProtobufRemotePlayer player) => _player = player;

    /// <inheritdoc />
    public ValueTask ShowCharacterListAsync()
    {
        if (_player.Account is not { } account)
        {
            return ValueTask.CompletedTask;
        }

        var list = new S2CCharacterList();
        foreach (var c in account.Characters.OrderBy(ch => ch.CharacterSlot))
        {
            list.Characters.Add(new CharacterInfo
            {
                Slot = c.CharacterSlot,
                Name = c.Name,
                Level = (uint)(c.Attributes.FirstOrDefault(a => a.Definition == Stats.Level)?.Value ?? 1),
                ClassNumber = (uint)(c.CharacterClass?.Number ?? 0),
                ClassName = c.CharacterClass?.Name ?? string.Empty,
            });
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { CharacterList = list });
    }
}
