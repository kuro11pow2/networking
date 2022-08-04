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

    public class ReliableKpSocket : KpSocket
    {
        private readonly ConcurrentQueue<ReliableMessage> _workQueue = new();
        private readonly ConcurrentQueue<ReliableMessage> _receiveQueue = new();
        private readonly ConcurrentDictionary<int, bool> _receiveWaitMids = new();
        private Dictionary<MessageType, int> _sendMsgTypeCount, _receiveMsgTypeCount;
        private int _sendMsgCount, _receiveMsgCount;


        public int SendMsgCount { get { return _sendMsgCount; } }
        public int ReceiveMsgCount { get { return _receiveMsgCount; } }
        public Dictionary<MessageType, int> SendMsgTypeCount { get { return _sendMsgTypeCount; } }
        public Dictionary<MessageType, int> ReceiveMsgTypeCount { get { return _receiveMsgTypeCount; } }

        private readonly AsyncLock _mutex = new();

        public ReliableKpSocket(Socket connectedSocket) : base(connectedSocket)
        {
            _receiveMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgCount = 0;
            _receiveMsgCount = 0;
        }

        public ReliableKpSocket(string address, int port) : base(address, port)
        {
            _receiveMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgTypeCount = new Dictionary<MessageType, int>();
            _sendMsgCount = 0;
            _receiveMsgCount = 0;
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();
            _ = StartReceiveAsync();
            _ = StartProccessWorkAsync();
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
            Log.Print($"{msg}", LogLevel.INFO, context: $"{nameof(ReliableKpSocket)}{Id}-{nameof(SendAsync)}");

            ReliableMessage rMsg = (ReliableMessage)msg;
            _receiveWaitMids.TryAdd(rMsg.Mid, true);
            await base.SendAsync(rMsg);
        }


        public override async Task<Message> ReceiveAsync()
        {
            while (_state == KpSocketState.CONNECTED)
            {
                //Thread.Sleep(1);
                await Task.Delay(1);

                bool result = _receiveQueue.TryDequeue(out ReliableMessage? msg);

                if (result == false || msg == null)
                    continue;
                return msg;
            }

            throw new IOException("연결 종료됨");
        }


        private async Task StartProccessWorkAsync()
        {
            while (_state == KpSocketState.CONNECTED)
            {
                //Thread.Sleep(1);
                await Task.Delay(1);
                bool result = _workQueue.TryDequeue(out ReliableMessage? reqMsg);

                if (result == false || reqMsg == null)
                    continue;

                if (MessageType.RES_START < reqMsg.Type && reqMsg.Type < MessageType.RES_END)
                {
                    if (_receiveWaitMids.TryGetValue(reqMsg.Mid, out bool _))
                    {
                        Log.Print($"{reqMsg.Mid}번 메시지 수신 응답 확인", LogLevel.INFO, context: $"{nameof(ReliableKpSocket)}{Id}-{nameof(StartProccessWorkAsync)}");
                        _receiveWaitMids.Remove(reqMsg.Mid, out bool _);
                        continue;
                    }

                    string exs = $"잘못된 response 수신\n{reqMsg}";
                    Log.Print(exs, LogLevel.ERROR, context: $"{nameof(ReliableKpSocket)}{Id}-{nameof(StartProccessWorkAsync)}");
                    throw new IOException(exs);
                }

                _receiveQueue.Enqueue(reqMsg);
            }
        }

        private async Task StartReceiveAsync()
        {
            while (_state == KpSocketState.CONNECTED)
            {
                Message receiveMsg;
                try
                {
                    receiveMsg = await base.ReceiveAsync();
                }
                catch
                {
                    break;
                }

                ReliableMessage rMsg;

                try
                {
                    rMsg = new ReliableMessage(receiveMsg.FullBytes.ToArray(), receiveMsg.FullBytes.Length);
                }
                catch (Exception ex)
                {
                    throw new Exception("수신한 데이터가 ReliableMessage가 아님", ex);
                }

                using (await _mutex.LockAsync())
                {
                    AddDict(_receiveMsgTypeCount, rMsg.Type, 1);
                    _receiveMsgCount++;
                }

                Log.Print($"{rMsg}", LogLevel.INFO, context: $"{nameof(ReliableKpSocket)}{Id}-{nameof(StartReceiveAsync)}");
                _workQueue.Enqueue(rMsg);
            }

            base.Stop();
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
