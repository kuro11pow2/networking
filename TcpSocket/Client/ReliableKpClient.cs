using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    using Common;

    public class ReliableKpClient
    {
        private ReliableKpSocket _socket;

        public ReliableKpClient(string address, int port)
        {
            _socket = new(address, port);
        }

        public async Task StartAsync()
        {
            await _socket.StartAsync();
        }

        public void Stop()
        {
            _socket.Stop();
        }

        public async Task EchoAsync(string s)
        {
            await _socket.SendAsync(s, MessageType.REQ_ECHO);
        }

        public async Task BroadcastAsync(string s)
        {
            await _socket.SendAsync(s, MessageType.REQ_BROADCAST);
        }

        public async Task<ReliableMessage> ReceiveAsync()
        {
            ReliableMessage receiveMsg = (ReliableMessage) await _socket.ReceiveAsync();

            if (receiveMsg.Type != MessageType.MESSAGE)
            {
                string exs = $"잘못된 메시지 수신\n{receiveMsg}";
                Log.Print(exs, LogLevel.ERROR, context: $"{nameof(ReliableKpClient)}-{nameof(ReceiveAsync)}");
                throw new Exception(exs);
            }

            ReliableMessage responseMsg = new("", MessageType.RES_MSG, receiveMsg.Mid); // 수신 응답
            await _socket.SendAsync(responseMsg);

            return receiveMsg;
        }

        private string Dict2Str<T, U>(Dictionary<T, U> dict) 
            where T : notnull 
            where U : notnull
        {
            return string.Join("\n", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }

        protected virtual string Info()
        {
            return $"\n[Send]\n{Dict2Str(_socket.SendMsgTypeCount)}\n[Receive]\n{Dict2Str(_socket.ReceiveMsgTypeCount)}";
        }

        public async Task StartMonitorAsync(int delay = 5000)
        {
            while (true)
            {
                Log.Print(Info(), LogLevel.RETURN, $"{nameof(ReliableKpClient)} monitor");
                await Task.Delay(delay);
            }
        }
    }
}
