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
    /// </summary>
    public class ReliableKpServer : KpServer
    {
        public ReliableKpServer(int port) : base(port)
        {
        }

        protected override KpSocket GetSocket(Socket sock)
        {
            return new ReliableKpSocket(sock);
        }

        protected override void StartReceive(KpSocket client)
        {
            Task.Run(async () =>
            {
                KpSocket _client = client;
                while (true)
                {
                    Message receiveMsg;
                    try
                    {
                        receiveMsg = await _client.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }
                    Log.Print($"{_client.Id}=>all, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                    // 메시지를 파싱하는 로직을 ReliableKpServer에 추가
                    // > 메시지 타입에 따라 KpServer의 메서드를 호출 (Send, Broadcast 등)
                    // 성능 개선 필요
                    // > R-kpClient에 R-msg를 인자로 받는 함수 추가 (불필요한 객체 생성 방지)
                    //  ㄴ KpServer의 client 객체를 팩토리 패턴 or 제네릭으로 변경 필요

                    
                    //foreach (var dstClient in _kpSocks.Values)
                    //{
                    //    Log.Print($"{_client.Id}=>{dstClient.Id}, {receiveMsg}", LogLevel.INFO, context: $"{nameof(KpServer)}-{nameof(StartReceive)}");

                    //    _ = dstClient.SendAsync(receiveMsg);
                    //}
                }
                RemoveSocket(_client);
                _client.Stop();
            });
        }
    }
}
