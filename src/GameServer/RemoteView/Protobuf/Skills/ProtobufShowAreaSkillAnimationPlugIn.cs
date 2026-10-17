// <copyright file="ProtobufShowAreaSkillAnimationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;
using MUnique.OpenMU.Pathfinding;

internal sealed class ProtobufShowAreaSkillAnimationPlugIn : IShowAreaSkillAnimationPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowAreaSkillAnimationPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowAreaSkillAnimationAsync(Player player, Skill skill, Point point, byte rotation)
    {
        var center = Vector2F.FromTile(point);
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            AreaSkillAnimation = new S2CAreaSkillAnimation
            {
                AttackerId = player.Id,
                SkillId    = (uint)(skill.Number < 0 ? 0 : skill.Number),
                TargetX    = center.X,
                TargetY    = center.Y,
            },
        });
    }
}
