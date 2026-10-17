// <copyright file="ProtobufMovementSpeed.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Builds <see cref="S2CMovementSpeed"/> from the speed <see cref="GameTickLoop"/> actually applies.
/// Shared by the plugins that observe its inputs: character state (world entry), stat updates
/// (equipment and effects) and safezone transitions.
/// </summary>
internal static class ProtobufMovementSpeed
{
    // 1 tile = 8 server units (Vector2F scale).
    private const float ServerUnitsPerTile = 8f;

    /// <summary>Attributes whose change can change the effective movement speed.</summary>
    internal static readonly HashSet<AttributeDefinition> Inputs =
    [
        Stats.MovementSpeed, Stats.MovementSpeedUnderwater, Stats.MovementSpeedFactor, Stats.IsUnderwater,
    ];

    internal static ServerEnvelope Build(Player player) => new()
    {
        MovementSpeed = new S2CMovementSpeed
        {
            MovementSpeed = (float)player.EffectiveMovementSpeed,
            TilesPerSec   = player.MovementSpeedServerUnitsPerSec / ServerUnitsPerTile,
        },
    };
}
