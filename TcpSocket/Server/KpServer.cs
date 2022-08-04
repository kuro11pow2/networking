﻿using System;
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
        PREPARED,
        STARTED,
        STOPPED,
    }

    public class KpServer
    {
        private Socket _listenSock;
        private IPEndPoint _ipEndPoint;
        protected ConcurrentDictionary<int, KpSocket> _kpSocks;
        private TcpServerState _state;
        private int _port;

        private readonly AsyncLock _mutex = new AsyncLock();
        private int _accSocketCount;

        public int ClientCount { get { return _kpSocks.Count; } }

        public KpServer(int port)
        {
            _port = port;
            Prepare();
        }

        protected virtual void Prepare()
        {
            _listenSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Any, _port);
            _listenSock.AddressFamily.ToString();
            _kpSocks = new ConcurrentDictionary<int, KpSocket>();
            _state = TcpServerState.PREPARED;
        }

        public void Start()
        {
            if (_state != TcpServerState.PREPARED)
                return;
            using (_mutex.Lock())
            {
                if (_state != TcpServerState.PREPARED)
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

                foreach (var client in _kpSocks.Values)
                {
                    RemoveSocket(client);
                    client.Stop();
                }

                try { _listenSock.Shutdown(SocketShutdown.Both); } catch { }
                try { _listenSock.Close(0); } catch { }

                _state = TcpServerState.STOPPED;
            }
        }

        private void ReadyAccept()
        {
            if (_state != TcpServerState.PREPARED)
                return;

            _listenSock.Bind(_ipEndPoint);
            _listenSock.Listen();
        }

        private void StartAccept()
        {
            if (_state != TcpServerState.PREPARED)
                return;

            Task.Run(async () =>
            {
                while (true)
                {
                    Socket sock = await _listenSock.AcceptAsync();
                    KpSocket kpSock = GetSocket(sock);
                    await AddSocketAsync(kpSock);
                    await kpSock.StartAsync();
                    StartReceive(kpSock);
                }
            });
        }

        protected virtual KpSocket GetSocket(Socket socket)
        {
            return new KpSocket(socket);
        }

        protected virtual void StartReceive(KpSocket _socket)
        {
            if (_state != TcpServerState.STARTED)
                return;

            Task.Run(async () =>
            {
                KpSocket socket = _socket;
                while (true)
                {
                    Message receiveMsg;
                    try
                    {
                        receiveMsg = await socket.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }
                    Log.Print($"{socket.Id}=>all, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                    foreach (var dstSock in _kpSocks.Values)
                    {
                        Log.Print($"{socket.Id}=>{dstSock.Id}, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                        _ = dstSock.SendAsync(receiveMsg);
                    }
                }
                RemoveSocket(socket);
                socket.Stop();
            });
        }

        private async Task AddSocketAsync(KpSocket socket)
        {
            bool res;
            
            using (await _mutex.LockAsync())
            {
                socket.Id = _accSocketCount++;
            }
            res = _kpSocks.TryAdd(socket.Id, socket);

            if (res)
                Log.Print($"추가됨 {socket.Id}", context: $"{nameof(KpServer)}-{nameof(AddSocketAsync)}");
            else
                Log.Print($"동일한 cid가 존재함 {socket.Id}", context: $"{nameof(KpServer)}-{nameof(AddSocketAsync)}");
        }

        protected void RemoveSocket(KpSocket socket)
        {
            bool res;
            int cid = socket.Id;
            KpSocket? removed;
            res = _kpSocks.TryRemove(cid, out removed);

            if (res)
                Log.Print($"제거됨 {socket.Id}", context: $"{nameof(KpServer)}-{nameof(RemoveSocket)}");
            else
                Log.Print($"이미 제거되거나 존재하지 않음 {socket.Id}", context: $"{nameof(KpServer)}-{nameof(RemoveSocket)}");
        }

        protected virtual string Info()
        {
            return $"\nCurSockCnt: {_kpSocks.Count}, AccSocketCnt: {_accSocketCount}";
        }

        public async Task StartMonitorAsync(int delay=5000)
        {
            while (_state == TcpServerState.STARTED)
            {
                Log.Print(Info(), LogLevel.RETURN, $"{nameof(ReliableKpServer)} monitor");
                await Task.Delay(delay);
            }
        }
    }
}
