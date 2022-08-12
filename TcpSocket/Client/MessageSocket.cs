using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Buffers;

using System.Runtime.InteropServices;

using Common;
using Nito.AsyncEx;

namespace Client
{
    public enum MessageSocketState
    {
        INIT,
        CREATED,
        CONNECTED,
        DISCONNECTED,
    }

    public enum ReliableLevel
    {
        UNRELIABLE,
        RELIABLE,
    }

    public class KpSocketOption
    {
        public ReliableLevel Level { get; set; }
        public KpSocketOption(ReliableLevel level = ReliableLevel.UNRELIABLE)
        {
            Level = level;
        }
    }

    public class MessageSocket
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _ipEndPoint;
        protected MessageSocketState _state;

        private readonly AsyncLock _mutex = new ();

        public int Id { get; set; }

        private NetworkStream _stream;

        public MessageSocket(string address, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _state = MessageSocketState.CREATED;
        }

        public MessageSocket(Socket connectedSocket)
        {
            _socket = connectedSocket;
            if (connectedSocket.RemoteEndPoint == null)
                throw Log.GetExceptionWithLog("connectedSocket.RemoteEndPoint != null", nameof(MessageSocket));
            _ipEndPoint = (IPEndPoint)connectedSocket.RemoteEndPoint;
            _state = MessageSocketState.CONNECTED;
        }

        public virtual async Task StartAsync()
        {
            await ConnectAsync();
            _stream = new NetworkStream(_socket, true);
        }

        public virtual void Stop()
        {
            Disconnect();
        }

        public virtual async Task SendAsync(Message msg)
        {
            if (_state != MessageSocketState.CONNECTED)
                throw Log.GetExceptionWithLog("_state != MessageSocketState.CONNECTED", nameof(SendAsync));

            await _stream.WriteAsync(msg.FullBytes);
            Log.Print(msg.ToString(), LogLevel.INFO, context: $"{nameof(MessageSocket)}{Id}-{nameof(SendAsync)}");
        }

        public virtual async Task<Message> ReceiveAsync()
        {
            if (_state != MessageSocketState.CONNECTED)
                throw Log.GetExceptionWithLog("_state != MessageSocketState.CONNECTED", nameof(ReceiveAsync));

            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            await ReadStreamAsync(buffer, 0, Message.HEADER_BYTES_LENGTH);
            int expectedBytesLength = Message.GetFullBytesLength(buffer);
            await ReadStreamAsync(buffer, Message.HEADER_BYTES_LENGTH, expectedBytesLength);
            var msg = new Message(buffer, Message.HEADER_BYTES_LENGTH + expectedBytesLength);
            ArrayPool<byte>.Shared.Return(buffer);

            Log.Print(msg.ToString(), LogLevel.INFO, context: $"{nameof(MessageSocket)}{Id}-{nameof(ReceiveAsync)}");

            return msg;
        }

        private async Task ReadStreamAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            int receivedBytes = await _stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

            if (receivedBytes == 0)
                throw Log.GetExceptionWithLog("0 byte 수신하여 종료", nameof(ReceiveAsync), level: LogLevel.INFO);
        }

        private async Task ConnectAsync()
        {
            if (!(_state == MessageSocketState.CREATED || _state == MessageSocketState.DISCONNECTED))
                return;
            using (await _mutex.LockAsync())
            {
                if (!(_state == MessageSocketState.CREATED || _state == MessageSocketState.DISCONNECTED))
                    return;
                await _socket.ConnectAsync(_ipEndPoint);

                _state = MessageSocketState.CONNECTED;
            }
        }

        private void Disconnect()
        {
            if (_state != MessageSocketState.CONNECTED)
                return;
            using (_mutex.Lock())
            {
                if (_state != MessageSocketState.CONNECTED)
                    return;
                try { _stream.Close(0); } catch { }

                _state = MessageSocketState.DISCONNECTED;
            }
        }

        private string GetSocketInfo()
        {
            var props = _socket.GetType().GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                object? value = null;
                try { value = p.GetValue(_socket, null); } catch { }

                sb.AppendLine(p.Name + ": " + (value ?? "null"));
            }
            return sb.ToString();
        }
    }
}
