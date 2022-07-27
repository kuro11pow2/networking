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

    public class TcpServer
    {
        private Socket _socket;
        private IPEndPoint _ipEndPoint;
        private ConcurrentDictionary<int, TcpClient> _clients;
        private readonly AsyncLock _mutex = new AsyncLock();
        private int _autoIncrementCid;

        public TcpServer(int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            _socket.AddressFamily.ToString();
            _clients = new ConcurrentDictionary<int, TcpClient>();
        }

        public void Start()
        {
            ReadyAccept();
            StartAccept();
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
                    TcpClient client = new TcpClient(sock);
                    await AddClientAsync(client);
                    await client.StartAsync();
                    StartReceive(client);
                }
            });
        }

        private void StartReceive(TcpClient client)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    string receiveMsg;
                    try
                    {
                        receiveMsg = await client.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }
                    Log.Print($"from: {client.Cid}, receiveMsg: {receiveMsg}", context: nameof(StartReceive));

                    foreach (var dstClient in _clients.Values)
                    {
                        _ = dstClient.SendAsync($"from: {client.Cid}, {receiveMsg}");
                    }
                }
                await RemoveClientAsync(client);
                await client.StopAsync();
            });
        }

        private async Task AddClientAsync(TcpClient client)
        {
            bool res;
            using (await _mutex.LockAsync())
            {
                client.Cid = _autoIncrementCid;
                res = _clients.TryAdd(client.Cid, client);
                _autoIncrementCid++;
            }
            if (res)
                Log.Print($"추가됨 {client.Cid}", context: nameof(AddClientAsync));
            else
                Log.Print($"동일한 cid가 존재함 {client.Cid}", context: nameof(AddClientAsync));
        }

        private async Task RemoveClientAsync(TcpClient client)
        {
            bool res;
            using (await _mutex.LockAsync())
            {
                int cid = client.Cid;
                TcpClient? removed;
                res = _clients.TryRemove(cid, out removed);
            }
            if (res)
                Log.Print($"제거됨 {client.Cid}", context: nameof(RemoveClientAsync));
            else
                Log.Print($"이미 제거되거나 존재하지 않음 {client.Cid}", context: nameof(RemoveClientAsync));
        }
    }
}
