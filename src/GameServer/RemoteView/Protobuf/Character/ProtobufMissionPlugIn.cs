// <copyright file="ProtobufMissionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Missions;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Missions;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// The missions for the Protobuf client: the mission log, its updates and the NPC dialog.
/// </summary>
internal sealed class ProtobufMissionPlugIn : IMissionViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufMissionPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowMissionLogAsync()
    {
        var msg = new S2CMissionLog();
        foreach (var state in MissionService.Active(_player))
        {
            msg.Missions.Add(this.ToData(state));
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { MissionLog = msg });
    }

    public ValueTask ShowMissionUpdatedAsync(CharacterMission mission) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { MissionUpdated = new S2CMissionUpdated { Mission = this.ToData(mission) } });

    public ValueTask ShowMissionRemovedAsync(short number) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { MissionRemoved = new S2CMissionRemoved { Number = (uint)number } });

    public ValueTask ShowMissionDialogAsync(MonsterDefinition npc, IReadOnlyList<MissionDefinition> offers, IReadOnlyList<MissionDefinition> turnIns)
    {
        var msg = new S2CMissionDialog { NpcName = npc.Designation.ToString() ?? string.Empty };
        msg.Offers.Add(offers.Select(ToOffer));
        msg.TurnIns.Add(turnIns.Select(ToOffer));
        return _player.SendEnvelopeAsync(new ServerEnvelope { MissionDialog = msg });
    }

    private static MissionOffer ToOffer(MissionDefinition mission)
    {
        var offer = new MissionOffer
        {
            Number = (uint)mission.Number,
            Name = mission.Name.ToString() ?? string.Empty,
            Description = mission.Description,
            Repeat = mission.Repeat == MissionRepeat.Once ? string.Empty : mission.Repeat.ToString().ToLowerInvariant(),
        };
        if (MissionService.Steps(mission).FirstOrDefault() is { } first)
        {
            offer.Objectives.Add(MissionService.Objectives(first).Select(MissionService.Describe));
        }

        offer.Rewards.Add(mission.Rewards.Select(MissionService.Describe));
        return offer;
    }

    private MissionData ToData(CharacterMission state)
    {
        var mission = state.Mission!;
        var steps = MissionService.Steps(mission);
        var data = new MissionData
        {
            Number = (uint)mission.Number,
            Name = mission.Name.ToString() ?? string.Empty,
            Description = mission.Description,
            StepIndex = (uint)state.StepIndex,
            StepCount = (uint)steps.Count,
            StepDescription = steps.ElementAtOrDefault(state.StepIndex)?.Description ?? string.Empty,
            Tracked = state.IsTracked,
            ReadyToTurnIn = MissionService.IsReadyToTurnIn(_player, state),
            TurnInNpc = MissionService.Receiver(mission)?.Designation.ToString() ?? string.Empty,
        };
        foreach (var objective in MissionService.ObjectiveStates(_player, state))
        {
            data.Objectives.Add(new MissionObjectiveData
            {
                Description = MissionService.Describe(objective.Objective),
                Current = (uint)objective.Current,
                Required = (uint)objective.Required,
                Done = objective.IsDone,
            });
        }

        return data;
    }
}
