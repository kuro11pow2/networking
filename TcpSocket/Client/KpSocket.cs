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
    public enum KpSocketState
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

    public class KpSocket
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _ipEndPoint;
        protected KpSocketState _state;

        private readonly AsyncLock _mutex = new ();

        public int Id { get; set; }

        private NetworkStream? _stream;
        private NetworkStream Stream 
        { 
            get 
            { 
                if (_stream == null) 
                    _stream = new NetworkStream(_socket);
                return _stream;
            } 
            set {  _stream = value; } 
        }

        public KpSocket(string address, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _state = KpSocketState.CREATED;
        }

        public KpSocket(Socket connectedSocket)
        {
            _socket = connectedSocket;
            if (connectedSocket.RemoteEndPoint == null)
                throw new Exception("_socket.RemoteEndPoint is null");
            _ipEndPoint = (IPEndPoint)connectedSocket.RemoteEndPoint;
            _state = KpSocketState.CONNECTED;
        }

        public virtual async Task StartAsync()
        {
            await ConnectAsync();
        }

        public virtual void Stop()
        {
            Disconnect();
        }

        public virtual async Task SendAsync(Message msg)
        {
            await Stream.WriteAsync(msg.FullBytes);
            Log.Print(msg.ToString(), LogLevel.INFO, context: $"{nameof(KpSocket)}{Id}-{nameof(SendAsync)}");
        }

        public virtual async Task<Message> ReceiveAsync()
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            await ReadStreamAsync(buffer, 0, Message.HEADER_BYTES_LENGTH);
            int expectedBytesLength = Message.GetFullBytesLength(buffer);
            await ReadStreamAsync(buffer, Message.HEADER_BYTES_LENGTH, expectedBytesLength);

            var msg = new Message(buffer, Message.HEADER_BYTES_LENGTH + expectedBytesLength);
            ArrayPool<byte>.Shared.Return(buffer);
            Log.Print(msg.ToString(), LogLevel.INFO, context: $"{nameof(KpSocket)}{Id}-{nameof(ReceiveAsync)}");

            return msg;
        }

        private async Task ReadStreamAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            int receivedMessageBytesLength = 0;

            while (true)
            {
                var mem = new Memory<byte>(buffer, offset + receivedMessageBytesLength, count - receivedMessageBytesLength);
                int currentReceived = await Stream.ReadAsync(mem, cancellationToken);
                receivedMessageBytesLength += currentReceived;

                if (currentReceived == 0)
                {
                    string ex = "0 byte 수신하여 종료";
                    Log.Print(ex, LogLevel.INFO);
                    throw new IOException(ex);
                }
                if (receivedMessageBytesLength > count)
                {
                    string ex = "받기로 한 것보다 많은 메시지 바이트를 수신함";
                    Log.Print(ex, LogLevel.WARN);
                    throw new IOException(ex);
                }
                if (receivedMessageBytesLength < count)
                {
                    continue;
                }

                break;
            }
        }

        private async Task ConnectAsync()
        {
            if (!(_state == KpSocketState.CREATED || _state == KpSocketState.DISCONNECTED))
                return;
            using (await _mutex.LockAsync())
            {
                if (!(_state == KpSocketState.CREATED || _state == KpSocketState.DISCONNECTED))
                    return;
                await _socket.ConnectAsync(_ipEndPoint);

                _state = KpSocketState.CONNECTED;
            }
        }

        private void Disconnect()
        {
            if (_state != KpSocketState.CONNECTED)
                return;
            using (_mutex.Lock())
            {
                if (_state != KpSocketState.CONNECTED)
                    return;
                try { _socket.Shutdown(SocketShutdown.Both); _socket.Close(0); } catch { }
                try { Stream.Close(0); _stream = null; } catch { }

                _state = KpSocketState.DISCONNECTED;
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
