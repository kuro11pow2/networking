
using Server;
using Common;

int port = 12345;

//Log.PrintLevel = LogLevel.WARN;

Log.PrintHeader();
Log.Print("시작", LogLevel.RETURN);

#if RELEASE
const string filePath = "room_config.json";
Object2File<Config> object2FileHelper = new Object2File<Config>(filePath);
Config config;

try
{
    config = await object2FileHelper.Load();
}
catch (FileNotFoundException ex)
{
    config = new Config();
    await object2FileHelper.Save(config);
}
#elif DEBUG
Config config = new();
#endif

Log.PrintLevel = config.PrintLevel;

await ReliableKpServerTest(config.Port);

Log.Print("끝", LogLevel.RETURN);


async Task KpServerTest(int port)
{
    MessageServer ks = new MessageServer(port);

    ks.Start();

    await ks.StartMonitorAsync();
}

async Task ReliableKpServerTest(int port)
{
    ReliableMessageServer ks = new ReliableMessageServer(port);

    ks.Start();

    await ks.StartMonitorAsync(10000);
}