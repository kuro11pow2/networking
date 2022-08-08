using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Nito.AsyncEx;
using System.Collections.Concurrent;

namespace Client
{
    using Common;

    public class ReliableMessageSocket : MessageSocket
    {
        private readonly ConcurrentDictionary<int, bool> _receiveWaitMids = new();
        private Dictionary<MessageType, int> _sendMsgTypeCount, _receiveMsgTypeCount;
        private int _sendMsgCount, _receiveMsgCount;


        public int SendMsgCount { get { return _sendMsgCount; } }
        public int ReceiveMsgCount { get { return _receiveMsgCount; } }
        public Dictionary<MessageType, int> SendMsgTypeCount { get { return _sendMsgTypeCount; } }
        public Dictionary<MessageType, int> ReceiveMsgTypeCount { get { return _receiveMsgTypeCount; } }

        private readonly AsyncLock _mutex = new();

        public ReliableMessageSocket(Socket connectedSocket) : base(connectedSocket)
        {
            _receiveMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgCount = 0;
            _receiveMsgCount = 0;
        }

        public ReliableMessageSocket(string address, int port) : base(address, port)
        {
            _receiveMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgCount = 0;
            _receiveMsgCount = 0;
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();
        }

        public override void Stop()
        {
            base.Stop();
        }

        public async Task SendAsync(string s, MessageType type)
        {
            int mid;

            using (await _mutex.LockAsync())
            {
                AddDict(_sendMsgTypeCount, type, 1);
                mid = _sendMsgCount++;
            }

            ReliableMessage msg = new(s, type, mid);
            await SendAsync(msg);
        }


        public override async Task SendAsync(Message msg)
        {
            Log.Print($"{msg}", LogLevel.INFO, context: $"{nameof(ReliableMessageSocket)}{Id}-{nameof(SendAsync)}");

            ReliableMessage rMsg = (ReliableMessage)msg;
            _receiveWaitMids.TryAdd(rMsg.Mid, true);
            await base.SendAsync(rMsg);
        }


        public override async Task<Message> ReceiveAsync()
        {
            while (_state == KpSocketState.CONNECTED)
            {
                Message msg = await base.ReceiveAsync();
                ReliableMessage rMsg;
                try
                {
                    rMsg = new ReliableMessage(msg.FullBytes.ToArray(), msg.FullBytes.Length);
                }
                catch (Exception ex)
                {
                    string exs1 = "수신한 데이터가 ReliableMessage가 아님";
                    Log.Print(exs1, LogLevel.ERROR, context: $"{nameof(ReliableMessageSocket)}{Id}-{nameof(ReceiveAsync)}");
                    throw new Exception(exs1);
                }

                using (await _mutex.LockAsync())
                {
                    AddDict(_receiveMsgTypeCount, rMsg.Type, 1);
                    _receiveMsgCount++;
                }

                Log.Print($"{rMsg}", LogLevel.INFO, context: $"{nameof(ReliableMessageSocket)}{Id}-{nameof(ReceiveAsync)}");
                ProccessMessage(rMsg, out bool validMsg);

                if (validMsg)
                    return rMsg;
            }

            string exs2 = "_state != KpSocketState.CONNECTED";
            Log.Print(exs2, LogLevel.ERROR, context: $"{nameof(ReliableMessageSocket)}{Id}-{nameof(ReceiveAsync)}");
            throw new Exception(exs2);
        }

        private void ProccessMessage(ReliableMessage reqMsg, out bool isValid)
        {
            if (MessageType.RES_START < reqMsg.Type && reqMsg.Type < MessageType.RES_END)
            {
                if (_receiveWaitMids.TryGetValue(reqMsg.Mid, out bool _))
                {
                    Log.Print($"{reqMsg.Mid}번 메시지 수신 응답 확인", LogLevel.INFO, context: $"{nameof(ReliableMessageSocket)}{Id}-{nameof(ProccessMessage)}");
                    _receiveWaitMids.Remove(reqMsg.Mid, out bool _);
                    isValid = false;
                    return;
                }

                string exs = $"잘못된 response 수신\n{reqMsg}";
                Log.Print(exs, LogLevel.ERROR, context: $"{nameof(ReliableMessageSocket)}{Id}-{nameof(ProccessMessage)}");
                throw new IOException(exs);
            }
            isValid = true;
        }

        private void AddDict<T>(Dictionary<T, int> dict, T key, int ebx) where T : notnull
        {
            if (dict.TryGetValue(key, out int eax))
                dict[key] = eax + ebx;
            else
                dict[key] = ebx;
        }
    }
}
