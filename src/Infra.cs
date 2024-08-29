using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;

namespace codecrafters_redis;

public class Infra
{
    public ConcurrentBag<Slave> slaves =  new ConcurrentBag<Slave>();
    public ConcurrentBag<Client> clients = new ConcurrentBag<Client>();
}


public class BaseClient
{
    public TcpClient socket;
    public IPEndPoint remoteIpEndPoint;
    public NetworkStream stream;
    public int port;
    public string ipAddress;
    public int id;

    public async Task SendAsync(string response)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
    }
    public async Task Send(byte[] bytes)
    {
        await stream.WriteAsync(bytes);
    }

    public async Task Send(string response, byte[] bytes)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response));

        await stream.WriteAsync(bytes);
    }
}

public class Client: BaseClient
{
    public Client(TcpClient socket, IPEndPoint ip, NetworkStream stream, int id)
    {
        this.socket = socket;
        this.stream = stream;
        this.id = id;
        remoteIpEndPoint = ip;
        ipAddress = remoteIpEndPoint.Address.ToString();
        port = remoteIpEndPoint.Port;
    }
}

public class Slave
{
    public List<string> capabilities;
    public Client connection;
    public int id;
    public Slave(int id, Client client)
    {
        this.id = id;
        capabilities = new List<string>();
        connection = client;
    }
}