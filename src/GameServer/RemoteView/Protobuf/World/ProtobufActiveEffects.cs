// <copyright file="ProtobufActiveEffects.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Adds the effects an entity already has when it comes into view; later changes come as
/// S2CBuffApplied / S2CBuffExpired.
/// </summary>
internal static class ProtobufActiveEffects
{
    internal static void AddTo(EntityState state, IAttackable attackable)
    {
        foreach (var effect in attackable.MagicEffectList.VisibleEffects)
        {
            state.Effects.Add(new ActiveEffect
            {
                EffectId = (uint)(ushort)effect.Id,
                RemainingMs = (uint)effect.Remaining.TotalMilliseconds,
            });
        }
    }
}
