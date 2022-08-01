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
    /// 메시지 타입에 따라서 다르게 처리해야하므로 대부분의 함수를 오버라이드 해서 재구현 해야함
    /// 지금은 수신응답(RES) 메시지를 브로드캐스팅해버리는 버그가 있음.
    /// 그리고 Message를 ReliableMessage로 변환하는 과정이 매우 빈번하게 발생하여 구조 개선이 필요함.
    ///  이거는 어쩔 수 없을 듯. 
    /// </summary>
    public class ReliableKpServer : KpServer
    {
        public ReliableKpServer(int port) : base(port)
        {
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

                    // Message 객체의 byte를 조사해서 Message인지 ReliableMessage 확인해서 처리하는 로직이 ReliableKpSocket의 send에 있음
                    //  > ReliableKpServer에서 최대한 재정의를 덜하려고 이렇게 해봤는데 저 로직은 너무 비효율적임
                    //  > ReliableKpSocket에서 처리하지 말고 ReliableKpServer에서 처리해야 함.
                    //  > 메시지 타입에 따라 KpServer의 메서드를 호출 (Send, Broadcast 등)

                    // > R-kpClient에 R-msg를 인자로 받는 함수 추가 (불필요한 객체 생성 방지)
                    //  ㄴ KpServer의 client 객체를 팩토리 패턴 or 제네릭으로 변경 필요


                    //foreach (var dstClient in _kpSocks.Values)
                    //{
                    //    Log.Print($"{_client.Id}=>{dstClient.Id}, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                    //    _ = dstClient.SendAsync(receiveMsg);
                    //}
                }
                RemoveSocket(socket);
                socket.Stop();
            });
        }
    }
}
