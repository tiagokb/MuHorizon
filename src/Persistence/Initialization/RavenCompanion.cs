// <copyright file="RavenCompanion.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The Dark Lord's raven as a companion in the world: a monster definition for its body (health,
/// defense, ranges, respawn delay). It hits with the owner's raven stats, not with these.
/// </summary>
internal static class RavenCompanion
{
    /// <summary>
    /// Adds the raven companion monster, if it's missing.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.Any(m => m.Number == Player.RavenCompanionNumber))
        {
            return;
        }

        var monster = context.CreateNew<MonsterDefinition>();
        monster.SetGuid(Player.RavenCompanionNumber);
        gameConfiguration.Monsters.Add(monster);
        monster.Number = Player.RavenCompanionNumber;
        monster.Designation = "Dark Raven";
        monster.ObjectKind = NpcObjectKind.Monster;
        monster.MoveRange = 3;
        monster.AttackRange = 4;
        monster.ViewRange = 7;
        monster.MoveDelay = TimeSpan.FromMilliseconds(300);
        monster.AttackDelay = TimeSpan.FromMilliseconds(1500);
        monster.RespawnDelay = TimeSpan.FromSeconds(30);
        var attributes = new Dictionary<AttributeDefinition, float>
        {
            { Stats.Level, 1 },
            { Stats.MaximumHealth, 3000 },
            { Stats.DefenseBase, 150 },
            { Stats.DefenseRatePvm, 150 },
        };
        monster.AddAttributes(attributes, context, gameConfiguration);
    }
}
