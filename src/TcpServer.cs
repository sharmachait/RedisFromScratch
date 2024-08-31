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
        
        Thread slaveThread = new Thread(async () => await InitiateSlaveryAsync());
        slaveThread.Start();
        await StartMasterForSlaveInstanceAsync();
    }

    public async Task StartMasterForSlaveInstanceAsync()
    {
        try
        {
            Console.WriteLine($"Server started at {_config.port}");

            while (true)
            {
                TcpClient socket = await _server.AcceptTcpClientAsync();
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
        NetworkStream stream = master.GetStream();
        await HandShake(stream);
        await StartMasterPropagation(master, stream);
    }
    public async Task HandShake(NetworkStream stream)
    {
        

        var lenListeningPort = _config.port.ToString().Length;
        var listeningPort = _config.port.ToString();
        var mystrings =
            new string[] { "*1\r\n$4\r\nPING\r\n",
                     "*3\r\n$8\r\nREPLCONF\r\n$14\r\nlistening-port\r\n$" +
                         lenListeningPort.ToString() + "\r\n" + listeningPort +
                         "\r\n",
                     "*3\r\n$8\r\nREPLCONF\r\n$4\r\ncapa\r\n$6\r\npsync2\r\n",
                     "*3\r\n$5\r\nPSYNC\r\n$1\r\n?\r\n$2\r\n-1\r\n" };
        var buffer = new byte[1024];
        foreach (var part in mystrings)
        {
            Console.WriteLine($"Sending: {part}");
            byte[] data = Encoding.ASCII.GetBytes(part);
            await stream.WriteAsync(data);
            await stream.ReadAsync(buffer, 0, buffer.Length);
            var response = Encoding.ASCII.GetString(buffer);
            response = response.Replace("\r\n", " ");
            Console.WriteLine($"Response: {response}");

            // if response starts with +FULLRESYNC, await the redis file
            // if (response.ToLower().Contains("+fullresync"))
            // {
            //   Console.WriteLine("Receiving full resync");
            //   _ = Task.Run(async () => {
            //     var buffer = new byte[1024];
            //     await client.ReceiveAsync(buffer);
            //     response = Encoding.ASCII.GetString(buffer);
            //     Console.WriteLine($"Response: {response}");
            //   });
            // }
        }


        //string[] pingCommand = ["PING"];
        
        //stream.Write(Encoding.UTF8.GetBytes(_parser.RespArray(pingCommand)));
        //byte[] buffer = new byte[client.ReceiveBufferSize];
        //int bytesRead = stream.Read(buffer, 0, buffer.Length);

        //string[] ReplconfPortCommand = ["REPLCONF", "listening-port", _config.port.ToString()];
        
        //stream.Write(Encoding.UTF8.GetBytes(_parser.RespArray(ReplconfPortCommand)));
        //buffer = new byte[client.ReceiveBufferSize];
        //bytesRead = stream.Read(buffer, 0, buffer.Length);
        
        //string[] ReplconfCapaCommand = ["REPLCONF", "capa", "psync2"];
        
        //stream.Write(Encoding.UTF8.GetBytes(_parser.RespArray(ReplconfCapaCommand)));
        //buffer = new byte[client.ReceiveBufferSize];
        //bytesRead = stream.Read(buffer, 0, buffer.Length);
        
        //string[] PsyncCommand = ["PSYNC", "?", "-1"];

        //stream.Write(Encoding.UTF8.GetBytes(_parser.RespArray(PsyncCommand)));
        //buffer = new byte[client.ReceiveBufferSize];
        //bytesRead = stream.Read(buffer, 0, buffer.Length);
        //string response = Encoding.UTF8.GetString(buffer);
        //Console.WriteLine("psync response full resync *********************************************************************************");
        //Console.WriteLine($"bytes read: {bytesRead}");
        //Console.WriteLine($"Response: {response}");

        //buffer = new byte[client.ReceiveBufferSize];
        //bytesRead = stream.Read(buffer, 0, buffer.Length);
        //response = Encoding.UTF8.GetString(buffer);
        //Console.WriteLine("psync response rdb file *********************************************************************************");
        //Console.WriteLine($"bytes read: {bytesRead}");
        //Console.WriteLine($"Response: {response}");
        //Console.WriteLine("response ended *********************************************************************************");
    }
    public async Task StartMasterPropagation(TcpClient ConnectionWithMaster, NetworkStream stream)
    {
        while (ConnectionWithMaster.Connected)
        {
            byte[] buffer = new byte[ConnectionWithMaster.ReceiveBufferSize];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                List<string[]> commands = _parser.Deserialize(buffer.Take(bytesRead).ToArray());

                foreach (string[] command in commands)
                {
                    Console.WriteLine("........................................................");
                    Console.WriteLine("Command from master: " + string.Join(" ", command));
                    string response = await _handler.HandleCommandsFromMaster(command, ConnectionWithMaster);
                }
            }
        }
    }

}