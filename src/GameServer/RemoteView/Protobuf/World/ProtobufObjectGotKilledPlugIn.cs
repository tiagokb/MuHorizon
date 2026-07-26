// <copyright file="ProtobufObjectGotKilledPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufObjectGotKilledPlugIn : IObjectGotKilledPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufObjectGotKilledPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ObjectGotKilledAsync(IAttackable killedObject, IAttacker? killerObject)
    {
        var entityId = killedObject is ILocateable killed ? killed.Id : 0u;
        var killerId = killerObject is ILocateable killer ? killer.Id : 0u;
        var name = killedObject is NonPlayerCharacter npc ? npc.Definition?.Designation.ToString() ?? "?" : "player";
        _player.Logger.LogInformation("[Phase5] Entity died — id={EntityId} name={Name} killer={KillerId}", entityId, name, killerId);

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            EntityDied = new S2CEntityDied { EntityId = entityId, KillerId = killerId },
        });
    }
}
