﻿namespace Tests
{
    public class UnitTest1
    {
        public async Task ServerTest(string input, string expected)
        {
        }

        [Theory]
        [InlineData(@"0123", @"0123")]
        [InlineData(@"abcxyz", @"abcxyz")]
        [InlineData(@"@!#$%^()[]", @"@!#$%^()[]")]
        [InlineData(@"가나다뀕팛", @"가나다뀕팛")]
        [InlineData(@"😂🤣⛴🛬🎁", @"😂🤣⛴🛬🎁")]
        [InlineData(@"1111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000999", @"1111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000999")]
        [InlineData(@"1234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234", @"1234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234")]
        public async Task LocalServerTest(string input, string expected)
        {
            string address = "127.0.0.1";
            int port = 12345;

            MessageServer server = new MessageServer(port);
            server.Start();

            var msg = new Message(input);

            int count = 100;
            List<MessageSocket> sockets = new List<MessageSocket>();

            for (int i = 0; i < count; i++)
            {
                MessageSocket socket = new MessageSocket(address, port);
                socket.Id = i;
                _ = socket.StartAsync();
                sockets.Add(socket);
            }

            while (server.ClientCount < count)
            {
                await Task.Delay(1);
            }

            for (int i = 0; i < count; i++)
            {
                _ = sockets[i].SendAsync(msg);
            }

            int passedCount = 0;
            int totalReceivedMsgLength = 0;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    var actual = await sockets[j].ReceiveAsync();
                    if (expected != actual.Msg)
                        Assert.True(false, $"i={i}, j={j}, expected={expected}, actual={actual.Msg}, passedCount={passedCount}, totalReceivedMsgLength={totalReceivedMsgLength}");
                    passedCount++;
                    totalReceivedMsgLength += actual.Msg.Length;
                }
            }

            for (int i = 0; i < count; i++)
            {
                sockets[i].Stop();
            }

            while (server.ClientCount > 0)
                await Task.Delay(1);

            server.Stop();
        }


        [Theory]
        [InlineData(@"0123", @"0123")]
        [InlineData(@"abcxyz", @"abcxyz")]
        [InlineData(@"@!#$%^()[]", @"@!#$%^()[]")]
        [InlineData(@"가나다뀕팛", @"가나다뀕팛")]
        [InlineData(@"😂🤣⛴🛬🎁", @"😂🤣⛴🛬🎁")]
        [InlineData(@"1111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000999", @"1111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000111100001111000011110000999")]
        [InlineData(@"1234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234", @"1234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234123412341234")]
        public async Task LocalReliableServerTest(string input, string expected)
        {
            string address = "127.0.0.1";
            int port = 12345;

            ReliableMessageServer server = new ReliableMessageServer(port);
            server.Start();

            int count = 100;
            List<ReliableMessageClient> clients = new List<ReliableMessageClient>();

            for (int i = 0; i < count; i++)
            {
                ReliableMessageClient client = new ReliableMessageClient(address, port);
                _ = client.StartAsync();
                clients.Add(client);
            }

            while (server.ClientCount < count)
            {
                await Task.Delay(1);
            }

            for (int i = 0; i < count; i++)
            {
                _ = clients[i].BroadcastAsync(input);
            }

            List<Task> tasks = new();

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                Task t = Task.Run(async () =>
                {
                    for (int j = 0; j < count; j++)
                    {
                        ReliableMessage actual = await clients[idx].ReceiveAsync();
                        if (expected != actual.Content)
                            Assert.True(false, $"i={idx}, j={j}, expected={expected}, actual={actual.Content}");
                    }
                });

                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < count; i++)
            {
                clients[i].Stop();
            }

            while (server.ClientCount > 0)
                await Task.Delay(1);

            server.Stop();
        }
    }
}