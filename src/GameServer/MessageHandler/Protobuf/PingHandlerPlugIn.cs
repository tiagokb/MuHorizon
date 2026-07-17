// <copyright file="PingHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Protobuf;

using System.Buffers;
using System.Buffers.Binary;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Handles <see cref="PingRequest"/> messages from Protobuf WebSocket clients.
/// Deserializes the incoming <see cref="ClientEnvelope"/>, and for a PingRequest responds
/// with a <see cref="ServerEnvelope"/> containing a <see cref="PingResponse"/> with both
/// the echoed client timestamp and the server's current timestamp.
/// </summary>
public sealed class PingHandlerPlugIn
{
    private readonly ILogger<PingHandlerPlugIn> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PingHandlerPlugIn"/> class.
    /// </summary>
    public PingHandlerPlugIn(ILogger<PingHandlerPlugIn> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles a raw Protobuf packet received from the connection.
    /// </summary>
    /// <param name="connection">The connection that sent the packet.</param>
    /// <param name="packet">The raw Protobuf payload (without the 4-byte length prefix).</param>
    public async ValueTask HandleAsync(IConnection connection, ReadOnlySequence<byte> packet)
    {
        ClientEnvelope envelope;
        try
        {
            envelope = ClientEnvelope.Parser.ParseFrom(packet.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse ClientEnvelope from {EndPoint}", connection.EndPoint);
            return;
        }

        if (envelope.PayloadCase != ClientEnvelope.PayloadOneofCase.PingRequest)
        {
            _logger.LogDebug(
                "Received non-ping envelope (case {Case}) from {EndPoint}",
                envelope.PayloadCase,
                connection.EndPoint);
            return;
        }

        var serverTimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var response = new ServerEnvelope
        {
            PingResponse = new PingResponse
            {
                ClientTimestampMs = envelope.PingRequest.TimestampMs,
                ServerTimestampMs = serverTimestampMs,
            },
        };

        var payload = response.ToByteArray();

        using var locker = await connection.OutputLock.LockAsync().ConfigureAwait(false);

        Span<byte> lenBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)payload.Length);

        connection.Output.Write(lenBytes);
        connection.Output.Write(payload);
        await connection.Output.FlushAsync().ConfigureAwait(false);

        _logger.LogDebug(
            "PingResponse → {EndPoint}  clientTs={ClientTs}  serverTs={ServerTs}",
            connection.EndPoint,
            envelope.PingRequest.TimestampMs,
            serverTimestampMs);
    }
}
