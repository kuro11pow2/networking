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
        private int _totalMsgCount, _sendMsgCount, _receiveMsgCount;

        public int TotalMsgCount { get { return _totalMsgCount; } }
        public int SendMsgCount { get { return _sendMsgCount; } }
        public int ReceiveMsgCount { get { return _receiveMsgCount; } }
                
        private readonly AsyncLock _mutex = new();

        public ReliableKpSocket(Socket connectedSocket) : base(connectedSocket)
        {
        }

        public ReliableKpSocket(string address, int port) : base(address, port)
        {
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

        public override async Task SendAsync(Message msg)
        {
            ReliableMessage reqMsg;
            int mid;

            using (await _mutex.LockAsync())
            {
                _totalMsgCount++;
                mid = _sendMsgCount++;
            }

            try
            {
                reqMsg = new ReliableMessage(msg.FullBytes.ToArray(), msg.FullBytes.Length);
            }
            catch
            {
                reqMsg = new ReliableMessage(msg.Msg, MessageType.SEND, mid);
            }

            _receiveWaitMids.TryAdd(reqMsg.Mid, true);
            await base.SendAsync(reqMsg);
        }

        public override async Task<Message> ReceiveAsync()
        {
            while (_state == KpSocketState.CONNECTED)
            {
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
                await Task.Delay(1);

                bool result = _workQueue.TryDequeue(out ReliableMessage? reqMsg);
                if (result == false || reqMsg == null)
                    continue;

                if (reqMsg.Type == MessageType.RES)
                {
                    if (_receiveWaitMids.TryGetValue(reqMsg.Mid, out bool _))
                    {
                        _receiveWaitMids.Remove(reqMsg.Mid, out bool _);
                        continue;
                    }

                    string exs = $"잘못된 response 수신\n{reqMsg}";
                    Log.Print(exs, LogLevel.ERROR, context: $"{nameof(ReliableKpSocket)}{Id}-{nameof(StartProccessWorkAsync)}");
                    throw new IOException(exs);
                }
                else if (reqMsg.Type == MessageType.SEND)
                {
                    ReliableMessage response = new("", MessageType.RES, reqMsg.Mid);
                    await base.SendAsync(response);
                    _receiveQueue.Enqueue(reqMsg);
                }
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
                Log.Print($"{receiveMsg}", LogLevel.INFO, context: $"{nameof(ReliableKpSocket)}{Id}-{nameof(StartReceiveAsync)}");

                ReliableMessage reqMsg;

                using (await _mutex.LockAsync())
                {
                    _receiveMsgCount++;
                    _totalMsgCount++;
                }

                reqMsg = new ReliableMessage(receiveMsg.FullBytes.ToArray(), receiveMsg.FullBytes.Length);

                _workQueue.Enqueue(reqMsg);
            }

            base.Stop();
        }
    }
}
