// <copyright file="ProtobufRemotePlayer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer;

using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameServer.MessageHandler.Protobuf;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Protobuf;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A player connected via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufRemotePlayer : Player
{
    private readonly ProtobufPacketDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufRemotePlayer"/> class.
    /// </summary>
    /// <param name="gameContext">The game server context.</param>
    /// <param name="connection">The WebSocket connection.</param>
    public ProtobufRemotePlayer(IGameServerContext gameContext, IConnection connection)
        : base(gameContext)
    {
        this.Connection = connection;
        this.IpAddress = ExtractIpAddress(connection.EndPoint);
        this._dispatcher = new ProtobufPacketDispatcher(this, gameContext.LoggerFactory.CreateLogger<ProtobufPacketDispatcher>());
        connection.PacketReceived += this.PacketReceivedAsync;
        connection.Disconnected += this.DisconnectAsync;
    }

    /// <summary>
    /// Gets the underlying WebSocket connection.
    /// </summary>
    internal IConnection? Connection { get; private set; }

    /// <summary>
    /// Gets the remote IP address of the client.
    /// </summary>
    public string? IpAddress { get; }

    /// <summary>
    /// Gets the game server context.
    /// </summary>
    internal IGameServerContext GameServerContext => (IGameServerContext)this.GameContext;

    /// <inheritdoc />
    protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer()
        => new RemoteView.Protobuf.ProtobufViewPlugInContainer(this);

    /// <inheritdoc />
    protected override async ValueTask InternalDisconnectAsync()
    {
        await base.InternalDisconnectAsync().ConfigureAwait(false);
        if (this.Connection is { Connected: true } conn)
        {
            await conn.DisconnectAsync().ConfigureAwait(false);
            conn.Dispose();
            this.Connection = null;
        }
    }

    /// <summary>
    /// Serializes the given envelope and writes it to the connection with a 4-byte big-endian length prefix.
    /// </summary>
    /// <param name="envelope">The server envelope to send.</param>
    internal async ValueTask SendEnvelopeAsync(ServerEnvelope envelope)
    {
        if (this.Connection is not { Connected: true } conn)
        {
            return;
        }

        var payload = envelope.ToByteArray();

        using var locker = await conn.OutputLock.LockAsync().ConfigureAwait(false);
        Span<byte> lenBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)payload.Length);
        conn.Output.Write(lenBytes);
        conn.Output.Write(payload);
        await conn.Output.FlushAsync().ConfigureAwait(false);
    }

    private static string? ExtractIpAddress(EndPoint? endPoint)
    {
        if (endPoint is not IPEndPoint ip)
        {
            return null;
        }

        return (ip.Address.IsIPv4MappedToIPv6 ? ip.Address.MapToIPv4() : ip.Address).ToString();
    }

    private async ValueTask PacketReceivedAsync(ReadOnlySequence<byte> sequence)
    {
        try
        {
            await this._dispatcher.DispatchAsync(sequence).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error processing incoming Protobuf packet");
        }
    }
}
