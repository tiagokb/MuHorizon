// <copyright file="ProtobufSafezonePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

// Handles two responsibilities with a single class to avoid cross-plugin coupling:
//   1. ISafezoneChangedPlugIn  — serialises and sends S2CSafezoneChanged to the client.
//   2. IObjectMovedPlugIn      — detects own-player tile changes and fires (1) when the
//      safe-zone status changes.  Other entities' movement is ignored.
internal sealed class ProtobufSafezonePlugIn : ISafezoneChangedPlugIn, IObjectMovedPlugIn
{
    private readonly ProtobufRemotePlayer _player;
    private bool _lastStatus = true; // prime with "in safezone" so first real check always sends

    public ProtobufSafezonePlugIn(ProtobufRemotePlayer player) => _player = player;

    // ── ISafezoneChangedPlugIn ────────────────────────────────────────────────

    public ValueTask SafezoneChangedAsync(bool isInSafezone)
    {
        _lastStatus = isInSafezone;
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SafezoneChanged = new S2CSafezoneChanged { InSafezone = isInSafezone },
        });
    }

    // ── IObjectMovedPlugIn ────────────────────────────────────────────────────

    public ValueTask ObjectMovedAsync(ILocateable movedObject, MoveType moveType)
    {
        // Only act when the own player moves; ignore monsters, NPCs, and other players.
        if (!ReferenceEquals(movedObject, _player)) return ValueTask.CompletedTask;

        var pos = _player.Position;
        var map = _player.CurrentMap;
        var isNow = map is not null && map.Terrain.SafezoneMap[pos.X, pos.Y];

        return isNow == _lastStatus ? ValueTask.CompletedTask : SafezoneChangedAsync(isNow);
    }
}
