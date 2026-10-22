// <copyright file="ProtobufTalentsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// The talent trees (S2CTalents): the class's master skills grouped by tree and rank, with the
/// invested levels, the values and whether a point can be invested (and why not).
/// </summary>
internal sealed class ProtobufTalentsPlugIn : ITalentsViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufTalentsPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowTalentsAsync(string? error) => SendAsync(_player, error);

    /// <summary>
    /// Sends the talent trees; also after a master level up (new points).
    /// </summary>
    internal static ValueTask SendAsync(ProtobufRemotePlayer player, string? error = null)
    {
        if (player.SelectedCharacter is not { } character || player.Attributes is not { } attributes)
        {
            return ValueTask.CompletedTask;
        }

        var masterLevel = (int)attributes[Stats.MasterLevel];
        var expTable = player.GameContext.MasterExperienceTable;
        var msg = new S2CTalents
        {
            Unlocked = character.CharacterClass?.IsMasterClass is true,
            MasterLevel = (uint)masterLevel,
            Points = (uint)Math.Max(0, character.MasterLevelUpPoints),
            MasterExperience = (ulong)Math.Max(0, character.MasterExperience),
            NextLevelExperience = masterLevel + 1 < expTable.Length ? (ulong)expTable[masterLevel + 1] : 0,
            Error = error ?? string.Empty,
        };

        foreach (var tree in TalentRules.GetTalents(player)
                     .GroupBy(t => t.MasterDefinition!.Root)
                     .OrderBy(g => g.Key?.Name.ToString()))
        {
            var treeMsg = new TalentTree { Name = tree.Key?.Name.ToString() ?? "Talents" };
            foreach (var talent in tree.OrderBy(t => t.MasterDefinition!.Rank).ThenBy(t => t.Number))
            {
                var definition = talent.MasterDefinition!;
                var level = TalentRules.GetLevel(player, talent);
                var why = TalentRules.WhyNotInvest(player, talent);
                var node = new TalentNode
                {
                    SkillNumber = (uint)talent.Number,
                    Name = talent.Name.ToString() ?? string.Empty,
                    Rank = definition.Rank,
                    Level = (uint)level,
                    MaxLevel = definition.MaximumLevel,
                    LearnCost = definition.MinimumLevel,
                    Value = level > 0 ? definition.CalculateDisplayValue(level) : 0,
                    NextValue = level < definition.MaximumLevel ? definition.CalculateDisplayValue(level + 1) : 0,
                    Attribute = definition.TargetAttribute?.Designation ?? string.Empty,
                    Upgrades = definition.ReplacedSkill?.Name.ToString() ?? string.Empty,
                    CanInvest = why is null,
                    Locked = why ?? string.Empty,
                };
                node.Requires.Add(definition.RequiredMasterSkills.Select(s => s.Name.ToString() ?? string.Empty));
                treeMsg.Nodes.Add(node);
            }

            msg.Trees.Add(treeMsg);
        }

        return player.SendEnvelopeAsync(new ServerEnvelope { Talents = msg });
    }
}
