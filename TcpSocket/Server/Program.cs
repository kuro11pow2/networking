
using Server;
using Common;

Log.PrintHeader();

Log.Print("시작");

TcpServer ts = new TcpServer(12345);

ts.Start();

await Task.Delay(1000000);

Log.Print("끝");