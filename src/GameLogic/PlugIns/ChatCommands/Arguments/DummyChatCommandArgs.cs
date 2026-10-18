// <copyright file="DummyChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by <see cref="DummyChatCommandPlugIn"/>.
/// </summary>
public class DummyChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the behavior ("punchbag", "stun") or "clear".
    /// </summary>
    [Argument("behavior")]
    public string? Behavior { get; set; }
}
