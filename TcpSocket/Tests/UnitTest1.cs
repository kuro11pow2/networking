﻿namespace Tests
{
    public class UnitTest1
    {
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

            KpServer ks = new KpServer(port);
            ks.Start();

            var msg = new Message(input);

            int count = 100;
            List<KpClient> clients = new List<KpClient>();

            for (int i = 0; i < count; i++)
            {
                KpClient kc = new KpClient(address, port);
                kc.Cid = i;
                _ = kc.StartAsync();
                clients.Add(kc);
            }

            while (ks.ClientCount < count)
            {
                await Task.Delay(1);
            }

            for (int i = 0; i < count; i++)
            {
                _ = clients[i].SendAsync(msg);
            }

            int passedCount = 0;
            int totalReceivedMsgLength = 0;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    var actual = await clients[j].ReceiveAsync();
                    if (expected != actual.Msg)
                        Assert.True(false, $"i={i}, j={j}, expected={expected}, actual={actual.Msg}, passedCount={passedCount}, totalReceivedMsgLength={totalReceivedMsgLength}");
                    passedCount++;
                    totalReceivedMsgLength += actual.Msg.Length;
                }
            }

            for (int i = 0; i < count; i++)
            {
                clients[i].Stop();
            }

            while (ks.ClientCount > 0)
                await Task.Delay(1);

            ks.Stop();
        }
    }
}