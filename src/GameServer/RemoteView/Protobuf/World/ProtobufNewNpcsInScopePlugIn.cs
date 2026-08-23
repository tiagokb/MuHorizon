// <copyright file="ProtobufNewNpcsInScopePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufNewNpcsInScopePlugIn : INewNpcsInScopePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufNewNpcsInScopePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask NewNpcsInScopeAsync(IEnumerable<NonPlayerCharacter> newObjects, bool isSpawned = true)
    {
        var msg = new S2CEntitiesInScope();
        foreach (var npc in newObjects)
        {
            msg.Entities.Add(BuildNpcState(npc));
        }

        if (msg.Entities.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { EntitiesInScope = msg });
    }

    private static EntityState BuildNpcState(NonPlayerCharacter npc)
    {
        var pos = Vector2F.FromTile(npc.Position);
        uint level = 0, maxHp = 0, curHp = 0;
        if (npc is AttackableNpcBase attackable)
        {
            level  = (uint)attackable.Attributes[Stats.Level];
            maxHp  = (uint)Math.Max(0f, attackable.Attributes[Stats.MaximumHealth]);
            curHp  = (uint)Math.Max(0f, attackable.Attributes[Stats.CurrentHealth]);
        }

        return new EntityState
        {
            Id            = npc.Id,
            X             = pos.X,
            Y             = pos.Y,
            ClassNumber   = (uint)(npc.Definition?.Number ?? 0),
            Name          = npc.Definition?.Designation ?? string.Empty,
            Level         = level,
            EntityType    = npc is Monster ? EntityType.Monster : EntityType.Npc,
            MaxHealth     = maxHp,
            CurrentHealth = curHp,
        };
    }
}
