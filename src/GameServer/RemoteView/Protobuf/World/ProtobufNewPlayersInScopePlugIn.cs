// <copyright file="ProtobufNewPlayersInScopePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufNewPlayersInScopePlugIn : INewPlayersInScopePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufNewPlayersInScopePlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask NewPlayersInScopeAsync(IEnumerable<Player> newObjects, bool isSpawned = true)
    {
        var players = newObjects.ToList();
        var msg = new S2CEntitiesInScope();
        foreach (var p in players)
        {
            msg.Entities.Add(BuildEntityState(p));
        }

        if (msg.Entities.Count == 0)
        {
            return;
        }

        await _player.SendEnvelopeAsync(new ServerEnvelope { EntitiesInScope = msg }).ConfigureAwait(false);

        // Their guilds, shown above them (S2CEntityGuild).
        foreach (var p in players.Where(p => p.GuildStatus is not null))
        {
            await _player.InvokeViewPlugInAsync<GameLogic.Views.Guild.IAssignPlayersToGuildPlugIn>(v => v.AssignPlayerToGuildAsync(p, true)).ConfigureAwait(false);
        }
    }

    internal static EntityState BuildEntityState(Player p)
    {
        var state = new EntityState
        {
            Id = p.Id,
            X = p.ContinuousPosition.X,
            Y = p.ContinuousPosition.Y,
            ClassNumber = (uint)(p.SelectedCharacter?.CharacterClass?.Number ?? 0),
            Name = p.Name,
            Level = (uint)p.Level,
            PvpEnabled = p.PvpEnabled,
        };
        if (p.Attributes is { } attributes)
        {
            state.CurrentHealth = (uint)Math.Max(0, attributes[Stats.CurrentHealth]);
            state.MaxHealth = (uint)Math.Max(0, attributes[Stats.MaximumHealth]);
            state.CurrentShield = (uint)Math.Max(0, attributes[Stats.CurrentShield]);
            state.MaxShield = (uint)Math.Max(0, attributes[Stats.MaximumShield]);
        }

        state.Equipment.AddRange(ProtobufEquipment.Build(p));
        ProtobufActiveEffects.AddTo(state, p);
        return state;
    }
}
