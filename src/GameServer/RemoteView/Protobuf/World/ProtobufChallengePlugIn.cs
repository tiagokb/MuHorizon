// <copyright file="ProtobufChallengePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Challenges;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Challenges for the Protobuf client: the request popup and the challenge state (score, round,
/// countdown, who's out).
/// </summary>
internal sealed class ProtobufChallengePlugIn : IChallengeViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufChallengePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowChallengeRequestAsync(Player challenger, int challengerTeamSize, int ownTeamSize, int bestOf) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ChallengeRequest = new S2CChallengeRequest
            {
                ChallengerName = challenger.Name,
                ChallengerTeam = (uint)challengerTeamSize,
                OwnTeam = (uint)ownTeamSize,
                BestOf = (uint)bestOf,
            },
        });

    public ValueTask ShowChallengeStateAsync(Challenge? challenge, Player viewer)
    {
        if (challenge is null)
        {
            return _player.SendEnvelopeAsync(new ServerEnvelope { ChallengeState = new S2CChallengeState() });
        }

        var ownTeam = challenge.TeamOf(viewer);
        var msg = new S2CChallengeState
        {
            Active = true,
            Phase = (Network.Protobuf.ChallengePhase)challenge.Phase,
            Round = (uint)challenge.Round,
            BestOf = (uint)challenge.BestOf,
            OwnScore = (uint)(ownTeam == 0 ? challenge.ScoreA : challenge.ScoreB),
            EnemyScore = (uint)(ownTeam == 0 ? challenge.ScoreB : challenge.ScoreA),
            SecondsLeft = (uint)Math.Max(0, Math.Ceiling((challenge.PhaseEndsAt - DateTime.UtcNow).TotalSeconds)),
            Won = challenge.Winner == ownTeam,
        };
        foreach (var player in ownTeam == 0 ? challenge.TeamA : challenge.TeamB)
        {
            msg.OwnTeam.Add(new ChallengeFighter { Name = player.Name, Out = challenge.Out.Contains(player) });
        }

        foreach (var player in ownTeam == 0 ? challenge.TeamB : challenge.TeamA)
        {
            msg.EnemyTeam.Add(new ChallengeFighter { Name = player.Name, Out = challenge.Out.Contains(player) });
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { ChallengeState = msg });
    }
}
