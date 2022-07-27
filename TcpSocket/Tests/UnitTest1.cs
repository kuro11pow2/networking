namespace Tests
{
    public class UnitTest1
    {
        [Theory]
        [InlineData(@"0123", @"0123")]
        [InlineData(@"abcxyz", @"abcxyz")]
        public void Test1(string input, string expected)
        {
            int port = 12345;
            TcpServer ts = new TcpServer(port);
            ts.Start();
            
            // ts.StartMonitor();
            // ts.Stop();
        }
    }
}