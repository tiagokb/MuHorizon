// <copyright file="ProtobufCompanionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// The own companion (S2CCompanion) for the companion control in the HUD.
/// </summary>
internal sealed class ProtobufCompanionPlugIn : ICompanionViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufCompanionPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowCompanionAsync(Monster? companion, GameLogic.NPC.CompanionMode mode) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            Companion = companion is null
                ? new S2CCompanion()
                : new S2CCompanion
                {
                    Active = true,
                    EntityId = companion.Id,
                    Name = companion.Definition.Designation.ToString() ?? string.Empty,
                    Mode = (Network.Protobuf.CompanionMode)mode,
                },
        });
}
