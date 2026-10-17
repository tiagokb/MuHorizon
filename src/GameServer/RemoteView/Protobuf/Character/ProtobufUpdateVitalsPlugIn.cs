// <copyright file="ProtobufUpdateVitalsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufUpdateVitalsPlugIn : IUpdateStatsPlugIn
{
    // Only react to changes in the eight vital attributes; ignore combat/defensive stats.
    private static readonly HashSet<AttributeDefinition> VitalAttributes =
    [
        Stats.CurrentHealth, Stats.MaximumHealth,
        Stats.CurrentMana,   Stats.MaximumMana,
        Stats.CurrentShield, Stats.MaximumShield,
        Stats.CurrentAbility, Stats.MaximumAbility,
    ];

    private readonly ProtobufRemotePlayer _player;

    public ProtobufUpdateVitalsPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask UpdateStatsAsync(AttributeDefinition attribute, float value)
    {
        // This is the only IUpdateStatsPlugIn, so it also forwards movement and attack speed changes.
        if (ProtobufMovementSpeed.Inputs.Contains(attribute))
            return _player.SendEnvelopeAsync(ProtobufMovementSpeed.Build(_player));

        if (attribute == Stats.AttackSpeed || ProtobufAttackInterval.WeaponInputs.Contains(attribute))
            return ProtobufAttackInterval.SendAsync(_player);

        if (!VitalAttributes.Contains(attribute))
            return ValueTask.CompletedTask;

        var attrs = _player.Attributes!;
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            VitalsUpdate = new S2CVitalsUpdate
            {
                CurrentHp = (uint)Math.Max(0f, attrs[Stats.CurrentHealth]),
                MaxHp     = (uint)attrs[Stats.MaximumHealth],
                CurrentMp = (uint)Math.Max(0f, attrs[Stats.CurrentMana]),
                MaxMp     = (uint)attrs[Stats.MaximumMana],
                CurrentSd = (uint)Math.Max(0f, attrs[Stats.CurrentShield]),
                MaxSd     = (uint)attrs[Stats.MaximumShield],
                CurrentAg = (uint)Math.Max(0f, attrs[Stats.CurrentAbility]),
                MaxAg     = (uint)attrs[Stats.MaximumAbility],
            },
        });
    }
}
