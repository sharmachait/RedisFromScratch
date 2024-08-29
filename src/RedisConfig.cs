namespace codecrafters_redis;

public class RedisConfig
{
    public string role; 
    public int port;
    public int masterPort;
    public string masterHost;
    public string masterReplId;
    public long masterReplOffset;
    

    public RedisConfig(string role, int port, int masterPort, string masterHost)
    {
        this.role = role;
        this.port = port;
        this.masterHost = masterHost;
        this.masterPort = masterPort;
        masterReplId = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 8);
        masterReplOffset = 0;
    }

    public RedisConfig(int port)
    {
        role = "master";
        this.port = port;
        masterHost = ".";
        masterPort = int.MinValue;
        masterReplId = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 8);
        masterReplOffset = 0;
    }

    public RedisConfig()
    {
        role = "master";
        port = 6379;
        masterHost = ".";
        masterPort = int.MinValue;
        masterReplId = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 8);
        masterReplOffset = 0;
    }

}


