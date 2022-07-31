
using Client;
using Common;

Log.PrintLevel = LogLevel.WARN;

Log.PrintHeader();
Log.Print("시작", LogLevel.RETURN);

await RemoteServerTest();

Log.Print("끝", LogLevel.RETURN);

async Task RemoteServerTest()
{
    string address = "127.0.0.1";
    int port = 12345;

    string expected = "A";

    var msg = new Message(expected);

    int count = 20;
    List<ReliableKpSocket> clients = new List<ReliableKpSocket>();

    for (int i = 0; i < count; i++)
    {
        ReliableKpSocket kc = new ReliableKpSocket(address, port);
        kc.Id = i;
        await kc.StartAsync();
        clients.Add(kc);
    }

    Log.Print("서버 연결 대기중");
    await Task.Delay(1000);

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
            ReliableMessage actual = (ReliableMessage)await clients[j].ReceiveAsync();
            if (expected != actual.Content)
                throw new Exception($"i={i}, j={j}, expected={expected}, actual={actual.Content}, passedCount={passedCount}, totalReceivedMsgLength={totalReceivedMsgLength}");
            passedCount++;
            totalReceivedMsgLength += actual.Content.Length;
        }
    }

    Log.Print("서버 종료 시작");

    for (int i = 0; i < count; i++)
    {
        clients[i].Stop();
    }
}

async Task ClientTest()
{
    string address = "127.0.0.1";
    int port = 12345;

    ReliableKpSocket kc = new(address, port);

    await KpClientTest(kc, address, port);
}


async Task KpClientTest(KpSocket kc, string address, int port)
{

    while (true)
    {
        try
        {
            await kc.StartAsync();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    Message receiveMsg = await kc.ReceiveAsync();
                    Log.Print($"{receiveMsg}", context: "client");
                }
            });
            break;
        }
        catch (Exception ex)
        {
            Log.Print($"서버 연결 시도중 {address}:{port}\n{ex}");
            await Task.Delay(1000);
        }
    }


    while (true)
    {
        string s = Console.ReadLine() ?? "";
        var msg = new Message(s);
        try
        {
            await kc.SendAsync(msg);
        }
        catch
        {
            break;
        }
    }

    kc.Stop();
}