// <copyright file="ProtobufMessengerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Messenger;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Messenger;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Friends and letters for the Protobuf client: the friend list with online states, friend
/// requests, the letter box and reading letters; results as messages.
/// </summary>
internal sealed class ProtobufMessengerPlugIn :
    IInitializeMessengerPlugIn,
    IFriendAddedPlugIn,
    IFriendDeletedPlugIn,
    IFriendStateUpdatePlugIn,
    IShowFriendRequestPlugIn,
    IShowFriendInvitationResultPlugIn,
    IAddToLetterListPlugIn,
    ILetterDeletedPlugIn,
    ILetterSendResultPlugIn,
    IShowLetterPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufMessengerPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask InitializeMessengerAsync(MessengerInitializationData data, int maxLetters)
    {
        // Friends start offline; S2CFriendChanged follows for the online ones.
        var msg = new S2CMessengerInit { MaxLetters = (uint)Math.Max(0, maxLetters) };
        msg.Friends.Add(data.Friends.Select(name => new FriendData { Name = name }));
        msg.FriendRequests.Add(data.OpenFriendRequests);
        if (_player.SelectedCharacter is { } character)
        {
            for (var index = 0; index < character.Letters.Count; index++)
            {
                msg.Letters.Add(ToData(character.Letters[index], index));
            }
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { MessengerInit = msg });
    }

    public ValueTask FriendAddedAsync(string friendName) => this.SendFriendAsync(friendName, FriendChange.Added);

    public ValueTask FriendDeletedAsync(string deletedFriend) => this.SendFriendAsync(deletedFriend, FriendChange.Removed);

    public ValueTask FriendStateUpdateAsync(string friend, int serverId) =>
        this.SendFriendAsync(friend, serverId is (int)SpecialServerId.Offline or (int)SpecialServerId.Invisible ? FriendChange.Offline : FriendChange.Online);

    public ValueTask ShowFriendRequestAsync(string requester) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { FriendRequest = new S2CFriendRequest { RequesterName = requester } });

    // Chat room invitations of the legacy messenger; this client whispers instead.
    public ValueTask ShowFriendInvitationResultAsync(bool result, uint requestId) => ValueTask.CompletedTask;

    public ValueTask AddToLetterListAsync(LetterHeader letter, ushort index, bool newLetter) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            LetterAdded = new S2CLetterAdded { Letter = ToData(letter, index), IsNew = newLetter },
        });

    public ValueTask LetterDeletedAsync(ushort letterIndex) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { LetterDeleted = new S2CLetterDeleted { Index = letterIndex } });

    public ValueTask LetterSendResultAsync(LetterSendSuccess success, uint letterId)
    {
        var text = success switch
        {
            LetterSendSuccess.Success => "Letter sent.",
            LetterSendSuccess.MailboxFull => "The receiver's mailbox is full.",
            LetterSendSuccess.ReceiverNotExists => "There's no character with this name.",
            LetterSendSuccess.NotEnoughMoney => "Not enough zen to send the letter.",
            _ => "The letter couldn't be sent, try again.",
        };
        return _player.ShowBlueMessageAsync(text);
    }

    public ValueTask ShowLetterAsync(LetterBody letterBody) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            Letter = new S2CLetter
            {
                Id = letterBody.Header?.Id.ToString() ?? string.Empty,
                Sender = letterBody.Header?.SenderName ?? string.Empty,
                Title = letterBody.Header?.Subject ?? string.Empty,
                Message = letterBody.Message,
            },
        });

    private static LetterHeaderData ToData(LetterHeader letter, int index) => new()
    {
        Index = (uint)index,
        Id = letter.Id.ToString(),
        Sender = letter.SenderName ?? string.Empty,
        Title = letter.Subject ?? string.Empty,
        Date = letter.LetterDate.ToString("yyyy-MM-dd HH:mm"),
        Read = letter.ReadFlag,
    };

    private ValueTask SendFriendAsync(string name, FriendChange change) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { FriendChanged = new S2CFriendChanged { Name = name, Change = change } });
}
