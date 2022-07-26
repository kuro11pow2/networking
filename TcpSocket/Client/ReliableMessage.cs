﻿namespace Client
{
    public enum MessageType
    {
        MESSAGE = 0,

        REQ_START = 100,
        REQ_ECHO,
        REQ_BROADCAST,
        REQ_END,

        RES_START = 200,
        RES_MSG,
        RES_REQ,
        RES_END,
    }

    public class ReliableMessage : Message
    {
        public int Mid { get; }

        public MessageType Type { get; }

        public string Content { get; }

        public ReliableMessage(string content, MessageType type, int mid) : base($"{type},{mid},{content}")
        {
            Type = type;
            Mid = mid;
            Content = content;
        }

        public ReliableMessage(string content, byte[] msgBytes, MessageType type, int mid) : base($"{type},{mid},{content}", msgBytes)
        {
            Type = type;
            Mid = mid;
            Content = content;
        }

        public ReliableMessage(Span<byte> encodedMsgBytes, int fullBytesLength) : base(encodedMsgBytes, fullBytesLength)
        {
            string[] tokens = Msg.Split(",");
            Type = Enum.Parse<MessageType>(tokens[0]);
            Mid = Convert.ToInt32(tokens[1]);
            Content = string.Join("", tokens[2..]);
        }

        public override string ToString()
        {
            return $"{base.ToString()}\n[ReliableMessage] Type: {Type}, Mid: {Mid}, Content: {Content}";
        }
    }
}
