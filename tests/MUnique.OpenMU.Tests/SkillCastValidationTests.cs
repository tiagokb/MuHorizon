// <copyright file="SkillCastValidationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Inputs;
using MUnique.OpenMU.Pathfinding;
using NUnit.Framework;

/// <summary>
/// Tests the server-side validation of skill casts received over Protobuf:
/// only learned skills, only within range.
/// </summary>
[TestFixture]
public class SkillCastValidationTests
{
    private const short TeleportSkillNumber = 6;
    private const short TeleportRange = 6;
    private static readonly Point Start = new(100, 100);

    /// <summary>
    /// A skill the character didn't learn can't be cast, even if the client requests it.
    /// </summary>
    [Test]
    public async Task UnlearnedSkillIsNotCastableAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        Assert.That(SkillCastRules.GetCastableSkill(player, (ushort)TeleportSkillNumber), Is.Null);
    }

    /// <summary>
    /// A learned active skill is castable; a passive one isn't.
    /// </summary>
    [Test]
    public async Task LearnedActiveSkillIsCastableAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await LearnAsync(player, TeleportSkillNumber, TeleportRange, SkillType.Other).ConfigureAwait(false);
        await LearnAsync(player, 300, 0, SkillType.PassiveBoost).ConfigureAwait(false);

        Assert.That(SkillCastRules.GetCastableSkill(player, (ushort)TeleportSkillNumber), Is.Not.Null);
        Assert.That(SkillCastRules.GetCastableSkill(player, 300), Is.Null);
    }

    /// <summary>
    /// Teleport is rejected when the skill isn't learned.
    /// </summary>
    [Test]
    public async Task TeleportWithoutSkillIsRejectedAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);

        await new TeleportInput(new Point(103, 100)).HandleAsync(player).ConfigureAwait(false);

        Assert.That(player.IsTeleporting, Is.False);
    }

    /// <summary>
    /// Teleport is rejected beyond the skill range plus tolerance.
    /// </summary>
    [Test]
    public async Task TeleportBeyondRangeIsRejectedAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        await LearnAsync(player, TeleportSkillNumber, TeleportRange, SkillType.Other).ConfigureAwait(false);

        var tooFar = (byte)(Start.X + TeleportRange + SkillCastRules.RangeTolerance + 1);
        await new TeleportInput(new Point(tooFar, Start.Y)).HandleAsync(player).ConfigureAwait(false);

        Assert.That(player.IsTeleporting, Is.False);
    }

    /// <summary>
    /// Teleport within range starts the teleport.
    /// </summary>
    [Test]
    public async Task TeleportWithinRangeStartsAsync()
    {
        var player = await CreatePlayerAtStartAsync().ConfigureAwait(false);
        await LearnAsync(player, TeleportSkillNumber, TeleportRange, SkillType.Other).ConfigureAwait(false);

        await new TeleportInput(new Point((byte)(Start.X + TeleportRange), Start.Y)).HandleAsync(player).ConfigureAwait(false);

        Assert.That(player.IsTeleporting, Is.True);
    }

    private static async ValueTask<Player> CreatePlayerAtStartAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.IsAlive = true;
        player.Position = Start;
        return player;
    }

    private static ValueTask LearnAsync(Player player, short number, short range, SkillType type) =>
        player.SkillList!.AddLearnedSkillAsync(new TestSkill { Number = number, Range = range, SkillType = type });

    private sealed class TestSkill : Skill
    {
        public TestSkill()
        {
            this.Requirements = new List<AttributeRequirement>();
            this.ConsumeRequirements = new List<AttributeRequirement>();
        }
    }
}
