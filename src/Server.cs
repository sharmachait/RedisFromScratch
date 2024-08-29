using codecrafters_redis;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;

namespace codecrafters_redis;

class Program
{
    static async Task Main(string[] args)
    {
        RedisConfig config = new RedisConfig();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port":
                    config.port = int.Parse(args[i + 1]);
                    break;
                case "--replicaof":
                    config.role = "slave";
                    Console.WriteLine(args[i + 1]);
                    string masterHost = args[i + 1].Split(' ')[0];
                    int masterPort = int.Parse(args[i + 1].Split(' ')[1]);
                    config.masterHost = masterHost;
                    config.masterPort = masterPort;
                    break;
                default:
                    break;
            }
        }


        var serviceProvider = new ServiceCollection()
            .AddSingleton(config)
            .AddSingleton<Store>()
            .AddSingleton<Infra>()
            .AddSingleton<RespParser>()
            .AddSingleton<CommandHandler>()
            .AddSingleton<TcpServer>()
            .BuildServiceProvider();

        TcpServer app = serviceProvider.GetRequiredService<TcpServer>();

        var startTask = Task.Run(async () => await app.StartMasterAsync());
        if (config.role == "master")
        {
            await startTask;
        }
    }
}