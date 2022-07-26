
using Client;
using Common;

Log.PrintHeader();

Log.Print("시작");

string address = "127.0.0.1";
int port = 12345;
TcpClient tc = new(address, port);

while (true)
{
    try
    {
        await tc.StartAsync();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                string receiveMsg = await tc.ReceiveAsync();
                Log.Print($"receiveMsg: {receiveMsg}", context: "client");
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
    string? s = Console.ReadLine();
    try
    {
        await tc.SendAsync(s ?? "");
    }
    catch
    {
        break;
    }
}

await tc.StopAsync();

Log.Print("끝");