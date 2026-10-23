// <copyright file="IChallengeViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Challenges;

using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Shows challenges (see <see cref="ChallengeService"/>).
/// </summary>
public interface IChallengeViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a challenge request to the leader of the challenged team.
    /// </summary>
    /// <param name="challenger">The challenger.</param>
    /// <param name="challengerTeamSize">The size of the challenger's team.</param>
    /// <param name="ownTeamSize">The size of the challenged team.</param>
    /// <param name="bestOf">The number of rounds.</param>
    ValueTask ShowChallengeRequestAsync(Player challenger, int challengerTeamSize, int ownTeamSize, int bestOf);

    /// <summary>
    /// Shows the state of the challenge; <c>null</c> when it's over.
    /// </summary>
    /// <param name="challenge">The challenge.</param>
    /// <param name="viewer">The player who sees it (for "my team").</param>
    ValueTask ShowChallengeStateAsync(Challenge? challenge, Player viewer);
}
