// <copyright file="ProtobufPlayerVitalsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends another player's health and shield (S2CEntityVitals) for the bar above its head.
/// </summary>
internal sealed class ProtobufPlayerVitalsPlugIn : IPlayerVitalsPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufPlayerVitalsPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowVitalsAsync(Player player)
    {
        if (player.Attributes is not { } attributes)
        {
            return ValueTask.CompletedTask;
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            EntityVitals = new S2CEntityVitals
            {
                EntityId = player.Id,
                CurrentHealth = (uint)Math.Max(0, attributes[Stats.CurrentHealth]),
                MaxHealth = (uint)Math.Max(0, attributes[Stats.MaximumHealth]),
                CurrentShield = (uint)Math.Max(0, attributes[Stats.CurrentShield]),
                MaxShield = (uint)Math.Max(0, attributes[Stats.MaximumShield]),
            },
        });
    }
}
