// <copyright file="ProtobufPartyPlugIns.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Parties;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Party;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Shows a party invitation (S2CPartyRequest); the client answers with C2SPartyResponse.
/// </summary>
internal sealed class ProtobufShowPartyRequestPlugIn : IShowPartyRequestPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowPartyRequestPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowPartyRequestAsync(IPartyMember requester) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            PartyRequest = new S2CPartyRequest { RequesterId = requester.Id, RequesterName = requester.Name },
        });
}

/// <summary>
/// Sends the own party (S2CPartyList) when it changes, when a member leaves and when the members'
/// health changes; an empty list means the player isn't in a party anymore.
/// </summary>
internal sealed class ProtobufPartyListPlugIn : IUpdatePartyListPlugIn, IPartyHealthViewPlugIn, IPartyMemberRemovedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    // Members' health last sent, so the party's periodic health check only sends changes.
    private uint[] _lastHealth = [];

    public ProtobufPartyListPlugIn(ProtobufRemotePlayer player) => _player = player;

    public bool IsHealthUpdateNeeded() => !this.CurrentHealth().SequenceEqual(_lastHealth);

    public ValueTask UpdatePartyListAsync() => this.SendAsync();

    public ValueTask UpdatePartyHealthAsync() => this.SendAsync();

    public ValueTask PartyMemberRemovedAsync(byte index) => this.SendAsync();

    private uint[] CurrentHealth() =>
        _player.Party?.PartyList.Select(m => m.CurrentHealth).ToArray() ?? [];

    private ValueTask SendAsync()
    {
        _lastHealth = this.CurrentHealth();
        var msg = new S2CPartyList();

        // A member who is leaving is told while it still references the party: it gets an empty list.
        if (_player.Party is { } party && party.PartyList.Contains(_player))
        {
            var index = 0u;
            foreach (var member in party.PartyList)
            {
                var position = member is Player player ? player.ContinuousPosition : Vector2F.FromTile(member.Position);
                msg.Members.Add(new PartyMember
                {
                    Index = index++,
                    Id = member.IsConnected ? member.Id : 0u,
                    Name = member.Name,
                    MapId = member.CurrentMap?.MapId ?? 0,
                    X = position.X,
                    Y = position.Y,
                    CurrentHealth = member.CurrentHealth,
                    MaxHealth = member.MaximumHealth,
                });
            }
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { PartyList = msg });
    }
}
