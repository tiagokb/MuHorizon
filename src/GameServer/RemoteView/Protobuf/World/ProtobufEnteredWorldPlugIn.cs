// <copyright file="ProtobufEnteredWorldPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufEnteredWorldPlugIn : IEnteredWorldPlugIn
{
    // Built once on first world entry, reused forever. Avoids N+1 EF lazy-load on every login.
    private static ServerEnvelope? _cachedDefsEnvelope;
    private static readonly object _defsLock = new();

    private readonly ProtobufRemotePlayer _player;

    public ProtobufEnteredWorldPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask EnteredWorldAsync()
    {
        try
        {
            var defs = GetOrBuildDefs();
            await _player.SendEnvelopeAsync(defs).ConfigureAwait(false);
        }
        catch
        {
            // Item definitions are best-effort — a failure must not block world entry.
        }

        var map = _player.CurrentMap ?? throw new InvalidOperationException("Player has no current map.");
        var pos = _player.ContinuousPosition;

        await _player.SendEnvelopeAsync(new ServerEnvelope
        {
            EnteredWorld = new S2CEnteredWorld
            {
                MapId = map.MapId,
                X = pos.X,
                Y = pos.Y,
                OwnId = _player.Id,
                PvpMode = (PvpMapMode)map.Definition.PvpMode,
            },
        }).ConfigureAwait(false);

        // The player's id changed with the map: every member needs the new party list.
        if (_player.Party is { } party)
        {
            foreach (var member in party.PartyList)
            {
                await member.InvokeViewPlugInAsync<GameLogic.Views.Party.IUpdatePartyListPlugIn>(p => p.UpdatePartyListAsync()).ConfigureAwait(false);
            }
        }

        // Send initial safezone status so the client can set up combat/off-combat mode.
        var tile = _player.Position;
        var isInSafezone = map.Terrain.SafezoneMap[tile.X, tile.Y];
        await _player.InvokeViewPlugInAsync<ISafezoneChangedPlugIn>(
            p => p.SafezoneChangedAsync(isInSafezone)).ConfigureAwait(false);
    }

    private ServerEnvelope GetOrBuildDefs()
    {
        if (_cachedDefsEnvelope is not null)
            return _cachedDefsEnvelope;

        lock (_defsLock)
            return _cachedDefsEnvelope ??= BuildDefsEnvelope();
    }

    private ServerEnvelope BuildDefsEnvelope()
    {
        var msg = new S2CItemDefinitions();

        foreach (var item in _player.GameContext.Configuration.Items)
        {
            try
            {
                var isWearable = item.ItemSlot is not null;
                var data = new ItemDefinitionData
                {
                    Group        = item.Group,
                    Number       = (uint)item.Number,
                    Name         = item.GetNameForLevel(0),
                    Width        = item.Width,
                    Height       = item.Height,
                    DropLevel    = item.DropLevel,
                    ReqStrength  = GetReq(item, "Strength"),
                    ReqAgility   = GetReq(item, "Agility"),
                    ReqEnergy    = GetReq(item, "Energy"),
                    ReqVitality  = GetReq(item, "Vitality"),
                    ReqLeadership = GetReq(item, "Leadership"),
                    IsStackable  = !isWearable && item.Durability > 1,
                    IsConsumable = !isWearable,
                    AttackSpeed  = (uint)(item.BasePowerUpAttributes
                        .FirstOrDefault(p => p.TargetAttribute?.Id == Stats.AttackSpeedByWeapon.Id)
                        ?.BaseValue ?? 0),
                };

                try { foreach (var cc in item.QualifiedCharacters) data.QualifiedClassNumbers.Add(cc.Number); }
                catch { /* lazy-load unavailable for this item */ }

                try
                {
                    if (item.ItemSlot is { } slot)
                        foreach (var s in slot.ItemSlots)
                            data.EquipSlots.Add((uint)s);
                }
                catch { /* lazy-load unavailable for this item */ }

                msg.Items.Add(data);
            }
            catch { /* skip item entirely on unexpected failure */ }
        }

        return new ServerEnvelope { ItemDefinitions = msg };
    }

    private static uint GetReq(ItemDefinition item, string stat) =>
        (uint)(item.Requirements
            .FirstOrDefault(r => r.Attribute?.Designation?.Contains(stat, StringComparison.OrdinalIgnoreCase) == true)
            ?.MinimumValue ?? 0);
}
