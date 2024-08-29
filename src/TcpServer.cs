namespace codecrafters_redis;

using codecrafters_redis;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

class TcpServer
{
    public  TcpListener _server;
    private readonly RespParser _parser;
    private readonly CommandHandler _handler;
    private readonly RedisConfig _config;
    private readonly Infra _infra;
    private int id;


    public TcpServer(
        RedisConfig config
        , Store store
        , Infra infra
        , RespParser parser
        , CommandHandler handler
        )
    {
        _handler = handler;

        _parser = parser;

        _config = config;

        _infra = infra;

        id = 0;

        _server = new TcpListener(IPAddress.Any, config.port);
    }


    public async Task StartMasterAsync()
    {
        try
        {
            _server.Start();

            Console.WriteLine($"Server started at {_config.port}");

            while (true)
            {
                TcpClient socket = await _server.AcceptTcpClientAsync();
                //Console.WriteLine("Client id: " + id + " ***********************************");
                id++;
                IPEndPoint? remoteIpEndPoint = socket.Client.RemoteEndPoint as IPEndPoint;
                if (remoteIpEndPoint == null)
                    return;

                NetworkStream stream = socket.GetStream();

                Client client = new Client(socket, remoteIpEndPoint, stream, id);

                _infra.clients.Add(client);

                _ = Task.Run(async () => await HandleClientAsync(client));
            }
        }
        finally
        {
            //_infra.clients.Clear();
            //_infra.slaves.Clear();
            //_server.Stop();
            //_server.Dispose();
            //_server = null;
        }
    }
    public async Task HandleClientAsync(Client client)
    {
        while (client.socket.Connected)
        {
            byte[] buffer = new byte[client.socket.ReceiveBufferSize];
            int bytesRead = await client.stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                List<string[]> commands = _parser.Deserialize(buffer);

                foreach (string[] command in commands)
                {
                    Console.WriteLine("*****************************************************");
                    Console.WriteLine("Command from client: " + string.Join(" ", command));
                    ResponseDTO response = await _handler.Handle(command, client, DateTime.Now);
                    client.Send(response.response);
                    if (response.data != null)
                    {
                        client.Send(response.data);
                    }
                }
            }
        }
    }

    ////three threads
    //public async Task StartSlaveAsync()
    //{
    //    _server.Start();

    //    // Start a new thread for InitiateSlaveryAsync.
    //    Thread slaveThread = new Thread(async () => await InitiateSlaveryAsync());
    //    slaveThread.Start();

    //    // Start a new thread for StartMasterForSlaveInstanceAsync.
    //    Thread masterThread = new Thread(async () => await StartMasterForSlaveInstanceAsync());
    //    masterThread.Start();

    //    // The threads will continue running in the background.
    //    // You do not need to join them if either has an infinite loop.
    //}

    ////concurrency
    //public async Task StartSlaveAsync()
    //{
    //    _server.Start();
    //    Task ServerTask = Task.Run(async () => await StartMasterForSlaveInstanceAsync());
    //    Task SlaveTask = Task.Run(async () => await InitiateSlaveryAsync());

    //    await Task.WhenAll(ServerTask,SlaveTask);
    //}

    //two threads
    public async Task StartSlaveAsync()
    {
        _server.Start();

        // Start a new thread for InitiateSlaveryAsync.
        Thread slaveThread = new Thread(async () => await InitiateSlaveryAsync());
        slaveThread.Start();

        // StartMasterForSlaveInstanceAsync runs on the current thread.
        await StartMasterForSlaveInstanceAsync();

        // The thread for InitiateSlaveryAsync will continue running in the background.
        // You do not need to join it if StartMasterForSlaveInstanceAsync has an infinite loop.
    }

    public async Task StartMasterForSlaveInstanceAsync()
    {
        try
        {
            Console.WriteLine($"Server started at {_config.port}");

            while (true)
            {
                TcpClient socket = await _server.AcceptTcpClientAsync();
                //Console.WriteLine("Client id: " + id + " ***********************************");
                id++;
                IPEndPoint? remoteIpEndPoint = socket.Client.RemoteEndPoint as IPEndPoint;
                if (remoteIpEndPoint == null)
                    return;

                NetworkStream stream = socket.GetStream();

                Client client = new Client(socket, remoteIpEndPoint, stream, id);

                _infra.clients.Add(client);

                _ = Task.Run(async () => await HandleClientAsync(client));
            }
        }
        finally
        {
            //_infra.clients.Clear();
            //_infra.slaves.Clear();
            //_server.Stop();
            //_server.Dispose();
            //_server = null;
        }
    }

    public async Task InitiateSlaveryAsync()
    {
        TcpClient master = new TcpClient();
        await master.ConnectAsync(_config.masterHost, _config.masterPort);
        Console.WriteLine($"Replicating from {_config.masterHost}: {_config.masterPort}");
        HandShake(master);
        //StartMasterPropagation(master);
        //_ = Task.Run(async () => await StartMasterPropagation(master));
    }

    //done by slave instace
    //dont need to create the slave object here
    public async Task HandShake(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream, Encoding.UTF8);

        string[] pingCommand = ["PING"];
        
        stream.Write(Encoding.UTF8.GetBytes(_parser.RespArray(pingCommand)));
        string response = reader.ReadLine();
        if (!"+PONG".Equals(response))
        {
            Console.WriteLine(response);
            return;
        }
        Console.WriteLine($"Response: {response}");

        string[] ReplconfPortCommand = ["REPLCONF", "listening-port", _config.port.ToString()];
        
        await stream.WriteAsync(Encoding.UTF8.GetBytes(_parser.RespArray(ReplconfPortCommand)));
        response = await reader.ReadLineAsync();
        if (!"+OK".Equals(response))
        {
            Console.WriteLine(response);
            return;
        }
        Console.WriteLine($"Response: {response}");

        string[] ReplconfCapaCommand = ["REPLCONF", "capa", "psync2"];
        
        await stream.WriteAsync(Encoding.UTF8.GetBytes(_parser.RespArray(ReplconfCapaCommand)));
        response = await reader.ReadLineAsync();
        if (!"+OK".Equals(response))
        {
            Console.WriteLine(response);
            return;
        }
        Console.WriteLine($"Response: {response}");

        //Console.WriteLine("ready to process commands from master");

        string[] PsyncCommand = ["PSYNC", "?", "-1"];
        await stream.WriteAsync(Encoding.UTF8.GetBytes(_parser.RespArray(PsyncCommand)));
        response = await reader.ReadLineAsync();
        Console.WriteLine($"Response: {response}");

        //if (response == null || !"+FULLRESYNC".Equals(response.Substring(0, response.IndexOf(" "))))
        //    return null;

        //do multi thread to listen from master

    }

    //public async Task StartMasterPropagation(TcpClient ConnectionWithMaster)
    //{
    //    NetworkStream stream = ConnectionWithMaster.GetStream();
    //    while (ConnectionWithMaster.Connected)
    //    {
    //        if (stream.DataAvailable)
    //        {
    //            byte[] buffer = new byte[ConnectionWithMaster.ReceiveBufferSize];
    //            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

    //            if (bytesRead > 0)
    //            {
    //                List<string[]> commands = _parser.Deserialize(buffer.Take(bytesRead).ToArray());

    //                foreach (string[] command in commands)
    //                {

    //                    string response = await _handler.HandleMasterCommands(command);
    //                }
    //            }
    //        }
    //    }
    //}

}


