// <copyright file="WebSocketConnection.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

/// <summary>
/// An <see cref="IConnection"/> backed by an already-accepted <see cref="WebSocket"/>.
/// </summary>
/// <remarks>
/// Each packet is framed with a 4-byte big-endian length prefix followed by the payload.
/// TLS is terminated upstream by nginx; this class handles plain ws:// only.
/// </remarks>
public sealed class WebSocketConnection : IConnection
{
    private readonly WebSocket _webSocket;
    private readonly ILogger<WebSocketConnection> _logger;
    private readonly Pipe _outgoing = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketConnection"/> class.
    /// </summary>
    /// <param name="webSocket">The accepted WebSocket instance.</param>
    /// <param name="remoteEndPoint">The remote endpoint of the client.</param>
    /// <param name="logger">The logger.</param>
    public WebSocketConnection(WebSocket webSocket, EndPoint remoteEndPoint, ILogger<WebSocketConnection> logger)
    {
        _webSocket = webSocket;
        EndPoint = remoteEndPoint;
        _logger = logger;
        OutputLock = new AsyncLock();
        Output = _outgoing.Writer;
    }

    /// <inheritdoc />
    public event AsyncEventHandler<ReadOnlySequence<byte>>? PacketReceived;

    /// <inheritdoc />
    public event AsyncEventHandler? Disconnected;

    /// <inheritdoc />
    public bool Connected => !_disconnected && _webSocket.State == WebSocketState.Open;

    /// <inheritdoc />
    public EndPoint? EndPoint { get; }

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => null;

    /// <inheritdoc />
    public PipeWriter Output { get; }

    /// <inheritdoc />
    public AsyncLock OutputLock { get; }

    /// <inheritdoc />
    public async Task BeginReceiveAsync()
    {
        try
        {
            var receiveTask = RunReceiveLoopAsync();
            var sendTask = RunSendLoopAsync();
            await Task.WhenAny(receiveTask, sendTask).ConfigureAwait(false);
        }
        finally
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisconnectAsync()
    {
        if (_disconnected)
        {
            return;
        }

        _disconnected = true;
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while gracefully closing WebSocket for {EndPoint}", EndPoint);
        }

        await _outgoing.Writer.CompleteAsync().ConfigureAwait(false);
        await Disconnected.SafeInvokeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisconnectAsync().AsTask().WaitAndUnwrapException();
    }

    private async Task RunReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (!_disconnected && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket
                    .ReceiveAsync(buffer.AsMemory(), _cts.Token)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.Count < 4)
                {
                    continue;
                }

                // A single WebSocket message may contain multiple length-prefixed packets.
                var data = buffer.AsMemory(0, result.Count);
                var offset = 0;

                while (data.Length - offset >= 4)
                {
                    var payloadLength = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Span.Slice(offset));
                    if (data.Length - offset < 4 + payloadLength)
                    {
                        break;
                    }

                    // Copy payload before raising to avoid buffer-reuse issues.
                    var payloadCopy = data.Slice(offset + 4, payloadLength).ToArray();
                    await PacketReceived
                        .SafeInvokeAsync(new ReadOnlySequence<byte>(payloadCopy))
                        .ConfigureAwait(false);
                    offset += 4 + payloadLength;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket receive loop for {EndPoint}", EndPoint);
        }
    }

    private async Task RunSendLoopAsync()
    {
        var reader = _outgoing.Reader;
        try
        {
            while (true)
            {
                var readResult = await reader.ReadAsync(_cts.Token).ConfigureAwait(false);
                var data = readResult.Buffer;

                try
                {
                    if (data.Length > 0)
                    {
                        // Send all buffered bytes as a single WebSocket binary message.
                        // The caller is responsible for framing individual packets with the 4-byte prefix.
                        var bytes = data.ToArray();
                        await _webSocket
                            .SendAsync(bytes.AsMemory(), WebSocketMessageType.Binary, true, _cts.Token)
                            .ConfigureAwait(false);
                    }
                }
                finally
                {
                    reader.AdvanceTo(data.End);
                }

                if (readResult.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket send loop for {EndPoint}", EndPoint);
        }
    }
}
