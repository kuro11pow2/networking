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
    public enum TcpClientState
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

    public class KpClientOption
    {
        public ReliableLevel Level { get; set; }
        public KpClientOption(ReliableLevel level = ReliableLevel.UNRELIABLE)
        {
            Level = level;
        }
    }

    public class Message
    {
        public const int HEADER_BYTES_LENGTH = sizeof(int);

        private readonly string _msg;
        private readonly ReadOnlyMemory<byte> _fullbytes;

        public string Msg { get { return _msg; } }

        public ReadOnlyMemory<byte> FullBytes { get { return _fullbytes; } }

        public Message(string msg) : this(msg, GetMsgBytes(msg))
        {
        }

        public Message(string msg, byte[] msgBytes)
        {
            var fullBytesLength = sizeof(int) + (int)msgBytes.Length;
            var tmp = new byte[fullBytesLength];

            int dataBytesLength = (int)fullBytesLength - HEADER_BYTES_LENGTH;
            var headerBytes = GetHeaderBytes(dataBytesLength);

            Buffer.BlockCopy(headerBytes, 0, tmp, 0, HEADER_BYTES_LENGTH);
            Buffer.BlockCopy(msgBytes, 0, tmp, HEADER_BYTES_LENGTH, dataBytesLength);

            _msg = msg;
            _fullbytes = tmp;
        }

        public Message(Span<byte> encodedMsgBytes, int fullBytesLength)
        {
            _msg = GetMsg(encodedMsgBytes[HEADER_BYTES_LENGTH..fullBytesLength]);
            _fullbytes = encodedMsgBytes[..fullBytesLength].ToArray();
        }

        public override string ToString()
        {
            return _msg;
        }

        public static byte[] GetHeaderBytes(int fullBytesLength)
        {
            return BitConverter.GetBytes(fullBytesLength);
        }

        public static int GetFullBytesLength(byte[] headerBytes)
        {
            if (headerBytes.Length < HEADER_BYTES_LENGTH)
                throw new Exception($"헤더 사이즈 오류, expected {HEADER_BYTES_LENGTH}bytes, actual {headerBytes.Length}bytes");

            int ret = 0;
            for (int i = HEADER_BYTES_LENGTH - 1; i >= 0; i--)
            {
                ret <<= 8;
                ret += headerBytes[i];
            }
            return ret;
        }

        public static byte[] GetMsgBytes(string msg)
        {
            return Encoding.UTF8.GetBytes(msg);
        }

        public static string GetMsg(Span<byte> msgBytes)
        {
            return Encoding.UTF8.GetString(msgBytes);
        }
    }

    public class KpClient
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _ipEndPoint;
        private readonly KpClientOption _option;
        private TcpClientState _state;

        private readonly AsyncLock _mutex = new ();

        public int Cid { get; set; }

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

        public KpClient(string address, int port, KpClientOption? option=null)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _state = TcpClientState.CREATED;
            _option = option ?? new KpClientOption();
        }

        public KpClient(Socket connectedSocket, KpClientOption? option = null)
        {
            _socket = connectedSocket;
            if (connectedSocket.RemoteEndPoint == null)
                throw new Exception("_socket.RemoteEndPoint is null");
            _ipEndPoint = (IPEndPoint)connectedSocket.RemoteEndPoint;
            _state = TcpClientState.CONNECTED;
            _option = option ?? new KpClientOption();
        }

        public async Task StartAsync()
        {
            await ConnectAsync();
        }

        public void Stop()
        {
            Disconnect();
        }

        public async Task SendAsync(Message msg)
        {
            await Stream.WriteAsync(msg.FullBytes);
            Log.Print(msg.ToString(), LogLevel.INFO, context: $"{nameof(KpClient)}{Cid}-{nameof(SendAsync)}");
        }

        public async Task<Message> ReceiveAsync()
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            await ReadStreamAsync(buffer, 0, Message.HEADER_BYTES_LENGTH);
            int expectedBytesLength = Message.GetFullBytesLength(buffer);
            await ReadStreamAsync(buffer, Message.HEADER_BYTES_LENGTH, expectedBytesLength);

            var msg = new Message(buffer, Message.HEADER_BYTES_LENGTH + expectedBytesLength);
            ArrayPool<byte>.Shared.Return(buffer);
            Log.Print(msg.ToString(), LogLevel.INFO, context: $"{nameof(KpClient)}{Cid}-{nameof(ReceiveAsync)}");

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
                try { _socket.Shutdown(SocketShutdown.Both); _socket.Close(0); } catch { }
                try { Stream.Close(0); } catch { }

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
                try { value = p.GetValue(_socket, null); } catch { }

                sb.AppendLine(p.Name + ": " + (value ?? "null"));
            }
            return sb.ToString();
        }
    }
}
