// <copyright file="SkillCooldowns.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// The skill cooldowns of the MOBA-style client, used by the <see cref="SkillsInitializer"/>
/// and the update which adds them to existing databases.
/// Nova is left out: it consumes its skill once per charge stage, so it needs the casting
/// mechanic before it can get a cooldown.
/// </summary>
internal static class SkillCooldowns
{
    private static readonly IReadOnlyDictionary<SkillNumber, int> Milliseconds = new Dictionary<SkillNumber, int>
    {
        // Dark Wizard
        { SkillNumber.Poison, 1500 },
        { SkillNumber.Meteorite, 1500 },
        { SkillNumber.Lightning, 1500 },
        { SkillNumber.FireBall, 1500 },
        { SkillNumber.Flame, 2000 },
        { SkillNumber.Teleport, 3000 },
        { SkillNumber.Ice, 1500 },
        { SkillNumber.Twister, 2000 },
        { SkillNumber.EvilSpirit, 1500 },
        { SkillNumber.Hellfire, 2000 },
        { SkillNumber.PowerWave, 1000 },
        { SkillNumber.AquaBeam, 2000 },
        { SkillNumber.Cometfall, 3000 },
        { SkillNumber.Inferno, 4000 },
        { SkillNumber.TeleportAlly, 3000 },
        { SkillNumber.SoulBarrier, 5000 },
        { SkillNumber.EnergyBall, 1000 },
        { SkillNumber.Decay, 2000 },
        { SkillNumber.IceStorm, 2000 },

        // Dark Knight
        { SkillNumber.FallingSlash, 1000 },
        { SkillNumber.Lunge, 1000 },
        { SkillNumber.Uppercut, 1000 },
        { SkillNumber.Cyclone, 1500 },
        { SkillNumber.Slash, 1000 },
        { SkillNumber.TwistingSlash, 1500 },
        { SkillNumber.RagefulBlow, 2000 },
        { SkillNumber.DeathStab, 1000 },
        { SkillNumber.CrescentMoonSlash, 2000 },
        { SkillNumber.Impale, 1000 },
        { SkillNumber.SwellLife, 8000 },

        // Fairy Elf
        { SkillNumber.TripleShot, 1500 },
        { SkillNumber.Heal, 3000 },
        { SkillNumber.GreaterDefense, 8000 },
        { SkillNumber.GreaterDamage, 8000 },
        { SkillNumber.IceArrow, 1500 },
        { SkillNumber.Penetration, 1500 },
        { SkillNumber.InfinityArrow, 30000 },

        // Magic Gladiator
        { SkillNumber.FireSlash, 1000 },

        // Dark Lord
        { SkillNumber.Force, 1500 },
        { SkillNumber.FireBurst, 2000 },
        { SkillNumber.Earthshake, 3000 },
        { SkillNumber.ForceWave, 1500 },
        { SkillNumber.FireScream, 2000 },

        // Summoner
        { SkillNumber.DrainLife, 2000 },
        { SkillNumber.ChainLightning, 2000 },
        { SkillNumber.DamageReflection, 10000 },
        { SkillNumber.Berserker, 10000 },
        { SkillNumber.Sleep, 3000 },
        { SkillNumber.Weakness, 3000 },
        { SkillNumber.Innovation, 3000 },

        // Rage Fighter
        { SkillNumber.KillingBlow, 1000 },
        { SkillNumber.BeastUppercut, 1000 },
        { SkillNumber.ChainDrive, 1500 },
        { SkillNumber.DarkSide, 3000 },
        { SkillNumber.DragonRoar, 3000 },
        { SkillNumber.DragonSlasher, 2000 },
        { SkillNumber.Charge, 5000 },
    };

    /// <summary>
    /// Sets the cooldowns of the skills of the game configuration.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills)
        {
            if (Milliseconds.TryGetValue((SkillNumber)skill.Number, out var cooldown))
            {
                skill.CooldownMilliseconds = cooldown;
            }
        }
    }
}
