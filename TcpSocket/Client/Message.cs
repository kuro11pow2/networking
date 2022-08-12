using System.Text;


namespace Client
{
    using HEADER_TYPE = UInt16;

    public class Message
    {
        public const int HEADER_BYTES_LENGTH = sizeof(HEADER_TYPE);

        private readonly string _msg;
        private readonly ReadOnlyMemory<byte> _fullbytes;

        public string Msg { get { return _msg; } }

        public ReadOnlyMemory<byte> FullBytes { get { return _fullbytes; } }

        public Message(string msg) : this(msg, GetMsgBytes(msg))
        {
        }

        public Message(string msg, byte[] msgBytes)
        {
            var fullBytesLength = HEADER_BYTES_LENGTH + msgBytes.Length;
            var tmp = new byte[fullBytesLength];

            var headerBytes = GetHeaderBytes(msgBytes.Length);

            Buffer.BlockCopy(headerBytes, 0, tmp, 0, HEADER_BYTES_LENGTH);
            Buffer.BlockCopy(msgBytes, 0, tmp, HEADER_BYTES_LENGTH, msgBytes.Length);

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
}
