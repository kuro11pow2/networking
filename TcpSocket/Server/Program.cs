
using Server;
using Common;

Log.PrintHeader();

Log.Print("시작");

KpServer ks = new KpServer(12345);

ks.Start();

await Task.Delay(1000000);

Log.Print("끝");