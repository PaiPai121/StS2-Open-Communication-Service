using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Godot;

namespace SOCS.Code;

internal sealed class SocsServer
{
    private readonly ConcurrentDictionary<int, SocsClientConnection> _clients = new();
    private readonly Action<SocsInboundCommand, SocsClientConnection> _commandHandler;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _nextClientId;

    public SocsServer(Action<SocsInboundCommand, SocsClientConnection> commandHandler)
    {
        _commandHandler = commandHandler;
    }

    public void Start()
    {
        if (_cts != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, SocsConstants.Port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
        GD.Print($"SOCS listening on 127.0.0.1:{SocsConstants.Port}");
    }

    public void Stop()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();
        _listener?.Stop();
        _listener = null;

        foreach (SocsClientConnection client in _clients.Values)
        {
            client.Dispose();
        }

        _clients.Clear();
        _cts.Dispose();
        _cts = null;
    }

    public void Broadcast(ReadOnlyMemory<byte> payload)
    {
        foreach (SocsClientConnection client in _clients.Values)
        {
            _ = client.SendAsync(payload);
        }
    }

    public void SendResponse(SocsClientConnection client, object envelope)
    {
        byte[] payload = SocsProtocol.Serialize(envelope);
        _ = client.SendAsync(payload);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            TcpClient? tcpClient = null;
            try
            {
                tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                tcpClient.NoDelay = true;

                int clientId = Interlocked.Increment(ref _nextClientId);
                var connection = new SocsClientConnection(clientId, tcpClient, RemoveClient);
                _clients[clientId] = connection;
                _ = ReceiveLoopAsync(connection, cancellationToken);
                GD.Print($"SOCS client connected: {clientId}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"SOCS accept loop warning: {ex.Message}");
                tcpClient?.Dispose();
            }
        }
    }

    private async Task ReceiveLoopAsync(SocsClientConnection client, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[]? payload = await SocsProtocol.ReadFrameAsync(client.Stream, cancellationToken);
                if (payload == null)
                {
                    break;
                }

                SocsInboundCommand? command = SocsProtocol.Deserialize<SocsInboundCommand>(payload);
                if (command == null)
                {
                    SendResponse(client, new SocsErrorEnvelope { Message = "Invalid command payload." });
                    continue;
                }

                _commandHandler(command, client);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SOCS client receive warning: {ex.Message}");
            SendResponse(client, new SocsErrorEnvelope { Message = ex.Message });
        }
        finally
        {
            RemoveClient(client.Id);
        }
    }

    private void RemoveClient(int clientId)
    {
        if (_clients.TryRemove(clientId, out SocsClientConnection? client))
        {
            client.Dispose();
            GD.Print($"SOCS client disconnected: {clientId}");
        }
    }
}

internal sealed class SocsClientConnection : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Action<int> _removeClient;
    private int _disposed;

    public SocsClientConnection(int id, TcpClient client, Action<int> removeClient)
    {
        Id = id;
        Client = client;
        Stream = client.GetStream();
        _removeClient = removeClient;
    }

    public int Id { get; }
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }

    public async Task SendAsync(ReadOnlyMemory<byte> payload)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        await _sendLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            await SocsProtocol.WriteFrameAsync(Stream, payload, CancellationToken.None);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SOCS client send warning: {ex.Message}");
            _removeClient(Id);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stream.Dispose();
        Client.Dispose();
        _sendLock.Dispose();
    }
}
