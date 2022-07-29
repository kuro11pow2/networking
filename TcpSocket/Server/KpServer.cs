using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;
using Nito.AsyncEx;

namespace Server
{
    using Common;
    using Client;
    internal enum TcpServerState
    {
        INIT,
        CREATED,
        STARTED,
        STOPPED,
    }

    public class KpServer
    {
        private Socket _socket;
        private IPEndPoint _ipEndPoint;
        private ConcurrentDictionary<int, KpClient> _clients;
        private TcpServerState _state;

        private readonly AsyncLock _mutex = new AsyncLock();
        private int _autoIncrementCid;

        public int ClientCount { get { return _clients.Count; } }

        public KpServer(int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            _socket.AddressFamily.ToString();
            _clients = new ConcurrentDictionary<int, KpClient>();
            _state = TcpServerState.CREATED;
        }

        public void Start()
        {
            if (!(_state == TcpServerState.CREATED || _state == TcpServerState.STOPPED))
                return;
            using (_mutex.Lock())
            {
                if (!(_state == TcpServerState.CREATED || _state == TcpServerState.STOPPED))
                    return;
                ReadyAccept();
                StartAccept();
                _state = TcpServerState.STARTED;
            }
        }

        public void Stop()
        {
            if (_state != TcpServerState.STARTED)
                return;
            using (_mutex.Lock())
            {
                if (_state != TcpServerState.STARTED)
                    return;

                foreach (var client in _clients.Values)
                {
                    RemoveClient(client);
                    client.Stop();
                }

                try { _socket.Shutdown(SocketShutdown.Both); } catch { }
                try { _socket.Close(0); } catch { }

                _state = TcpServerState.STOPPED;
            }
        }

        private void ReadyAccept()
        {
            _socket.Bind(_ipEndPoint);
            _socket.Listen();
        }

        private void StartAccept()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    Socket sock = await _socket.AcceptAsync();
                    KpClient client = new KpClient(sock);
                    await AddClientAsync(client);
                    await client.StartAsync();
                    StartReceive(client);
                }
            });
        }

        private void StartReceive(KpClient client)
        {
            Task.Run(async () =>
            {
                KpClient _client = client;
                while (true)
                {
                    Message receiveMsg;
                    try
                    {
                        receiveMsg = await _client.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }
                    Log.Print($"{_client.Cid}=>all, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                    foreach (var dstClient in _clients.Values)
                    {
                        Log.Print($"{_client.Cid}=>{dstClient.Cid}, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                        _ = dstClient.SendAsync(receiveMsg);
                    }
                }
                RemoveClient(_client);
                _client.Stop();
            });
        }

        private async Task AddClientAsync(KpClient client)
        {
            bool res;
            
            using (await _mutex.LockAsync())
            {
                client.Cid = _autoIncrementCid++;
            }
            res = _clients.TryAdd(client.Cid, client);

            if (res)
                Log.Print($"추가됨 {client.Cid}", context: $"{nameof(KpServer)}-{nameof(AddClientAsync)}");
            else
                Log.Print($"동일한 cid가 존재함 {client.Cid}", context: $"{nameof(KpServer)}-{nameof(AddClientAsync)}");
        }

        private void RemoveClient(KpClient client)
        {
            bool res;
            int cid = client.Cid;
            KpClient? removed;
            res = _clients.TryRemove(cid, out removed);

            if (res)
                Log.Print($"제거됨 {client.Cid}", context: $"{nameof(KpServer)}-{nameof(RemoveClient)}");
            else
                Log.Print($"이미 제거되거나 존재하지 않음 {client.Cid}", context: $"{nameof(KpServer)}-{nameof(RemoveClient)}");
        }

        public async Task StartMonitorAsync()
        {
            while (_state == TcpServerState.STARTED)
            {
                Log.Print($"count: {_clients.Count}, acc count: {_autoIncrementCid}", LogLevel.RETURN, "server monitor");
                await Task.Delay(5000);
            }
        }
    }
}
