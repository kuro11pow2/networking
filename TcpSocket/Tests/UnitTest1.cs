namespace Tests
{
    public class UnitTest1
    {
        [Theory]
        [InlineData(@"abcxyz", @"abcxyz")]
        public async Task Test1(string input, string expected)
        {
            string address = "127.0.0.1";
            int port = 12345;
            TcpServer ts = new TcpServer(port);
            ts.Start();

            TcpClient tc = new TcpClient(address, port);
            tc.Cid = 0;
            await tc.StartAsync();

            await tc.SendAsync(input);
            string actual = await tc.ReceiveAsync();

            Assert.Equal(actual, $"from: {tc.Cid}, {expected}");

            // ts.StartMonitor();
            // ts.Stop();
        }
    }
}