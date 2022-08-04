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

    /// <summary>
    /// Message를 ReliableMessage로 변환하는 과정이 매우 빈번하게 발생하여 구조 개선이 필요함.
    /// </summary>
    public class ReliableKpServer : KpServer
    {
        public ReliableKpServer(int port) : base(port)
        {
        }

        protected override void Prepare()
        {
            base.Prepare();
        }
        private void AddDict<T>(Dictionary<T, int> dict, T key, int ebx) where T : notnull
        {
            if (dict.TryGetValue(key, out int eax))
                dict[key] = eax + ebx;
            else
                dict[key] = ebx;
        }
        private string Dict2Str<T, U>(Dictionary<T, U> dict)
            where T : notnull
            where U : notnull
        {
            return string.Join("\n", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }

        protected override string Info()
        {
            Dictionary<MessageType, int> SendMsgTypeCount = new Dictionary<MessageType, int>();
            Dictionary<MessageType, int> ReceiveMsgTypeCount = new Dictionary<MessageType, int>();

            foreach (ReliableKpSocket sock in _kpSocks.Values)
            {
                foreach (var tup in sock.SendMsgTypeCount)
                {
                    AddDict(SendMsgTypeCount, tup.Key, tup.Value);
                }
                foreach (var tup in sock.ReceiveMsgTypeCount)
                {
                    AddDict(ReceiveMsgTypeCount, tup.Key, tup.Value);
                }
            }
            return $"{base.Info()}\n[Send]\n{Dict2Str(SendMsgTypeCount)}\n[Receive]\n{Dict2Str(ReceiveMsgTypeCount)}";
        }

        protected override KpSocket GetSocket(Socket sock)
        {
            KpSocket socket = new ReliableKpSocket(sock);
            return socket;
        }

        protected override void StartReceive(KpSocket _socket)
        {
            Task.Run(async () =>
            {
                ReliableKpSocket socket = (ReliableKpSocket)_socket;
                while (true)
                {
                    ReliableMessage receiveMsg;
                    try
                    {
                        receiveMsg = (ReliableMessage)await socket.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }

                    if (receiveMsg.Type == MessageType.MESSAGE)
                    {
                        string exs = $"잘못된 메시지 수신\n{receiveMsg}";
                        Log.Print(exs, LogLevel.ERROR, context: $"{nameof(ReliableKpServer)}-{nameof(StartReceive)}");
                        throw new Exception(exs);
                    }

                    if (MessageType.REQ_START < receiveMsg.Type && receiveMsg.Type < MessageType.REQ_END)
                    {
                        if (receiveMsg.Type == MessageType.REQ_ECHO)
                        {
                            Log.Print($"{socket.Id}=>{socket.Id}, {receiveMsg.Content}", LogLevel.INFO, context: $"{nameof(ReliableKpServer)}-{nameof(StartReceive)}");
                            await socket.SendAsync(receiveMsg.Content, MessageType.MESSAGE);
                        }
                        else if (receiveMsg.Type == MessageType.REQ_BROADCAST)
                        {
                            Log.Print($"{socket.Id}=>all, {receiveMsg.Content}", LogLevel.INFO, context: $"{nameof(ReliableKpServer)}-{nameof(StartReceive)}");
                            foreach (ReliableKpSocket dstSock in _kpSocks.Values)
                            {
                                _ = dstSock.SendAsync(receiveMsg.Content, MessageType.MESSAGE);
                            }
                        }
                    }

                    ReliableMessage responseMsg = new("", MessageType.RES_REQ, receiveMsg.Mid); // 수신 응답
                    await socket.SendAsync(responseMsg);
                    continue;
                }
                RemoveSocket(socket);
                socket.Stop();
            });
        }
    }
}
