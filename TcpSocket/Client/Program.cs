
using Client;
using Common;

Log.PrintHeader();

Log.Print("시작");

string address = "127.0.0.1";
int port = 12345;
KpClient kc = new(address, port);

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

Log.Print("끝");