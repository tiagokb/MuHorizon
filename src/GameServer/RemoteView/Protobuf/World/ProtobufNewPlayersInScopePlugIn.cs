// <copyright file="ProtobufNewPlayersInScopePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufNewPlayersInScopePlugIn : INewPlayersInScopePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufNewPlayersInScopePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask NewPlayersInScopeAsync(IEnumerable<Player> newObjects, bool isSpawned = true)
    {
        var msg = new S2CEntitiesInScope();
        foreach (var p in newObjects)
        {
            msg.Entities.Add(BuildEntityState(p));
        }

        if (msg.Entities.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { EntitiesInScope = msg });
    }

    internal static EntityState BuildEntityState(Player p) => new()
    {
        Id = p.Id,
        X = p.Position.X,
        Y = p.Position.Y,
        ClassNumber = (uint)(p.SelectedCharacter?.CharacterClass?.Number ?? 0),
        Name = p.Name,
        Level = (uint)p.Level,
    };
}
