using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Common;
using Nito.AsyncEx;

namespace Client
{
    internal enum TcpClientState
    {
        INIT,
        CREATED,
        CONNECTED,
        DISCONNECTED,
        ERROR
    }

    public class TcpClient
    {
        private Socket _socket;
        private IPEndPoint _ipEndPoint;
        private TcpClientState _state;
        private readonly AsyncLock _mutex = new AsyncLock();
        public int Cid { get; set; }

        public TcpClient(string address, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _state = TcpClientState.CREATED;
        }

        public TcpClient(Socket connectedSocket)
        {
            _socket = connectedSocket;
            if (connectedSocket.RemoteEndPoint == null)
                throw new Exception("_socket.RemoteEndPoint is null");
            _ipEndPoint = (IPEndPoint)connectedSocket.RemoteEndPoint;
            _state = TcpClientState.CONNECTED;
        }

        public async Task StartAsync()
        {
            await ConnectAsync();
        }

        public void Stop()
        {
            Disconnect();
        }

        public async Task<int> SendAsync(string msg)
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            int sendBytesSize = await _socket.SendAsync(bytes, SocketFlags.None);
            return sendBytesSize;
        }

        public async Task<string> ReceiveAsync()
        {
            var buffer = new byte[8192];
            int receiveBytesSize = await _socket.ReceiveAsync(buffer, SocketFlags.None);
            string msg = Encoding.UTF8.GetString(buffer, 0, receiveBytesSize);
            return msg;
        }

        private async Task ConnectAsync()
        {
            if (!(_state == TcpClientState.CREATED || _state == TcpClientState.DISCONNECTED))
                return;
            using (await _mutex.LockAsync())
            {
                if (!(_state == TcpClientState.CREATED || _state == TcpClientState.DISCONNECTED))
                    return;
                await _socket.ConnectAsync(_ipEndPoint);
                _state = TcpClientState.CONNECTED;
            }
        }

        private void Disconnect()
        {
            if (_state != TcpClientState.CONNECTED)
                return;
            using (_mutex.Lock())
            {
                if (_state != TcpClientState.CONNECTED)
                    return;
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                try
                {
                    _socket.Close(0);
                }
                catch { }
                
                _state = TcpClientState.DISCONNECTED;
            }
        }

        private string GetSocketInfo()
        {
            var props = _socket.GetType().GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                object? value = null;
                try
                {
                    value = p.GetValue(_socket, null);
                }
                catch
                {
                }

                sb.AppendLine(p.Name + ": " + (value ?? "null"));
            }
            return sb.ToString();
        }
    }
}
