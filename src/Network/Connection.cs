// <copyright file="Connection.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using Pipelines.Sockets.Unofficial;

/// <summary>
/// A plain TCP <see cref="IConnection"/> backed by an <see cref="IDuplexPipe"/>.
/// No crypto — the legacy encryption layers have been removed.
/// </summary>
public sealed class Connection : PacketPipeReaderBase, IConnection
{
    private readonly IDuplexPipe _pipe;
    private readonly ILogger<Connection> _logger;
    private bool _disconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class.
    /// </summary>
    public Connection(IDuplexPipe pipe, ILogger<Connection> logger)
    {
        _pipe = pipe;
        Source = pipe.Input;
        _logger = logger;
        EndPoint = (pipe as SocketConnection)?.Socket?.RemoteEndPoint;
        LocalEndPoint = (pipe as SocketConnection)?.Socket?.LocalEndPoint;
    }

    /// <inheritdoc />
    public event AsyncEventHandler<ReadOnlySequence<byte>>? PacketReceived;

    /// <inheritdoc />
    public event AsyncEventHandler? Disconnected;

    /// <inheritdoc />
    public bool Connected => !_disconnected;

    /// <inheritdoc />
    public EndPoint? EndPoint { get; }

    /// <inheritdoc />
    public EndPoint? LocalEndPoint { get; }

    /// <inheritdoc />
    public PipeWriter Output => _pipe.Output;

    /// <inheritdoc />
    public AsyncLock OutputLock { get; } = new();

    /// <inheritdoc />
    public async Task BeginReceiveAsync()
    {
        try
        {
            await ReadSourceAsync().ConfigureAwait(false);
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
        await _pipe.Output.CompleteAsync().ConfigureAwait(false);
        await _pipe.Input.CompleteAsync().ConfigureAwait(false);
        await Disconnected.SafeInvokeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisconnectAsync().AsTask().WaitAndUnwrapException();
    }

    /// <inheritdoc />
    protected override async ValueTask<bool> ReadPacketAsync(ReadOnlySequence<byte> packet)
    {
        await PacketReceived.SafeInvokeAsync(packet).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    protected override ValueTask OnCompleteAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Connection error for {EndPoint}", EndPoint);
        }

        return ValueTask.CompletedTask;
    }
}