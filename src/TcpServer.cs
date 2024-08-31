namespace codecrafters_redis;

using codecrafters_redis;
using System;
using System.Diagnostics.Metrics;
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
        await StartListeningToMaster(master, stream);
        //await StartMasterPropagation(master, stream);
    }
    public async Task StartListeningToMaster(TcpClient master,NetworkStream stream)
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
        int c = 0;
        foreach (var part in mystrings)
        {
            Console.WriteLine($"Sending: {part}");
            byte[] data = Encoding.ASCII.GetBytes(part);
            await stream.WriteAsync(data);
            if (c == 3)
                break;
            await stream.ReadAsync(buffer, 0, buffer.Length);
            var response = Encoding.ASCII.GetString(buffer);
            c++;
        }
        List<byte> psyncReponse = new List<byte>();
        while (true)
        {
            var b = stream.ReadByte();
            psyncReponse.Add((byte)b);
            if (b == '*')
            {
                break;
            }
        }
        
        while (master.Connected)
        {
            int offset = 1;
            StringBuilder sb = new StringBuilder();
            List<byte> bytes = new List<byte>();

            while (true)
            {
                byte b =(byte)stream.ReadByte();
                if (b == '*')
                   break;
                
                offset++;
                bytes.Add(b);
                if (!stream.DataAvailable)
                    break;
            }

            sb.Append(Encoding.UTF8.GetString(bytes.ToArray()));

            Console.WriteLine(".........................................................");
            Console.WriteLine(bytes.Count);
            Console.WriteLine(sb.ToString());

            string command = sb.ToString();
            string[] parts = command.Split("\r\n");
            string[] commandArray = _parser.ParseArray(parts);

            string res = await _handler.HandleCommandsFromMaster(commandArray, master);

            if (commandArray[0].Equals("replconf") && commandArray[1].Equals("GETACK"))
            {
                offset++;
                List<byte> leftovercommand = new List<byte>();
                while (true)
                {
                    byte b = (byte)stream.ReadByte();
                    leftovercommand.Add(b);
                    if (b == '*')
                        break;

                    offset++;
                    
                    if (!stream.DataAvailable)
                        break;
                }
                Console.WriteLine("leftovercommand leftovercommand leftovercommand leftovercommand leftovercommand");
                Console.WriteLine(Encoding.UTF8.GetString(leftovercommand.ToArray()).Replace("\r\n","`r`n"));
                Console.WriteLine("leftovercommand leftovercommand leftovercommandleftovercommand leftovercommand");
                await stream.WriteAsync(Encoding.UTF8.GetBytes(res));
            }

            _config.masterReplOffset += offset;
        }
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
                    string response = await _handler.HandleCommandsFromMaster(command, ConnectionWithMaster);
                }
            }
        }
    }

}