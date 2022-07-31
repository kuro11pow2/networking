
using Server;
using Common;

int port = 12345;

Log.PrintLevel = LogLevel.WARN;

Log.PrintHeader();
Log.Print("시작", LogLevel.RETURN);

await ReliableKpServerTest(port);

Log.Print("끝", LogLevel.RETURN);


async Task KpServerTest(int port)
{
    KpServer ks = new KpServer(port);

    ks.Start();

    await Task.Delay(1000000);
}

async Task ReliableKpServerTest(int port)
{
    ReliableKpServer ks = new ReliableKpServer(port);

    ks.Start();

    await Task.Delay(1000000);
}