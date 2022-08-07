
using Client;
using Common;

//Log.PrintLevel = LogLevel.WARN;

Log.PrintHeader();
Log.Print("시작", LogLevel.RETURN);

string address = "192.168.0.53";
int port = 7000;

Log.PrintLevel = LogLevel.ERROR;

await KpClientTest(address, port);

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


async Task RunReliableKpClient(string address, int port)
{
    ReliableKpClient rkc = new(address, port);
    while (true)
    {
        try
        {
            await rkc.StartAsync();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    Message receiveMsg = await rkc.ReceiveAsync();
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

    _ = rkc.StartMonitorAsync();

    while (true)
    {
        string s = Console.ReadLine() ?? "";
        try
        {
            await rkc.BroadcastAsync(s);
        }
        catch
        {
            break;
        }
    }

    rkc.Stop();
}


async Task KpClientTest(string address, int port)
{
    string input = "가나다", expected = "가나다";

    int count = 100;
    List<ReliableKpClient> sockets = new List<ReliableKpClient>();

    for (int i = 0; i < count; i++)
    {
        ReliableKpClient socket = new ReliableKpClient(address, port);
        _ = socket.StartAsync();
        sockets.Add(socket);
    }

    // 서버 연결 대기
    await Task.Delay(2000);

    for (int i = 0; i < count; i++)
    {
        _ = sockets[i].BroadcastAsync(input);
    }

    int passedCount = 0;
    int totalReceivedMsgLength = 0;
    for (int i = 0; i < count; i++)
    {
        for (int j = 0; j < count; j++)
        {
            ReliableMessage actual = await sockets[j].ReceiveAsync();
            if (expected != actual.Content)
                Log.Print($"i={i}, j={j}, expected={expected}, actual={actual.Content}, passedCount={passedCount}, totalReceivedMsgLength={totalReceivedMsgLength}", LogLevel.ERROR);
            passedCount++;
            totalReceivedMsgLength += actual.Content.Length;
        }
    }

    Log.Print("메시지 처리 대기중");
    // 메시지 처리 대기
    await Task.Delay(2000);

    for (int i = 0; i < count; i++)
    {
        sockets[i].Stop();
    }
}


async Task KpClientAsyncTest(string address, int port)
{
    string input = "가나다", expected = "가나다";

    int count = 100;
    List<ReliableKpClient> clients = new List<ReliableKpClient>();

    for (int i = 0; i < count; i++)
    {
        ReliableKpClient socket = new ReliableKpClient(address, port);
        _ = socket.StartAsync();
        clients.Add(socket);
    }

    // 서버 연결 대기
    await Task.Delay(2000);

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
                {
                    Log.Print($"i ={ idx}, j ={ j}, expected ={ expected}, actual ={ actual.Content}", LogLevel.ERROR);
                }
            }
        });

        tasks.Add(t);
    }

    Log.Print("메시지 처리 대기중");
    await Task.WhenAll(tasks);

    for (int i = 0; i < count; i++)
    {
        clients[i].Stop();
    }
}


