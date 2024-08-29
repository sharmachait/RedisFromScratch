using System.Net.Sockets;
using System.Net;
using System.Text;

namespace codecrafters_redis;

public class Infra
{
    public List<Slave> slaves =  new List<Slave>();
    public List<Client> clients = new List<Client>();
}


public class BaseClient
{
    public TcpClient socket;
    public IPEndPoint remoteIpEndPoint;
    public NetworkStream stream;
    public int port;
    public string ipAddress;
    public int id;

    public void Send(string response)
    {
        stream.Write(Encoding.UTF8.GetBytes(response));
    }
    public void Send(byte[] bytes)
    {
        stream.Write(bytes);
    }

    public void Send(string response, byte[] bytes)
    {
        stream.Write(Encoding.UTF8.GetBytes(response));

        stream.Write(bytes);
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