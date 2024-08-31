﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_redis;

public class CommandHandler
{
    private readonly RespParser _parser;
    private readonly Store _store;
    private readonly RedisConfig _config;
    private readonly Infra _infra;
    private int slaveId = 0;

    public CommandHandler(Store store, RespParser parser, RedisConfig config, Infra infra)
    {
        _infra = infra;
        _parser = parser;
        _store = store;
        _config = config;
    }

    public async Task<string> HandleCommandsFromMaster(string[] command, TcpClient ConnectionWithMaster)
    {
        string cmd = command[0];

        DateTime currTime = DateTime.Now;
        string res = "";

        switch (cmd)
        {
            case "set":
                res = _store.Set(command);
                _ = Task.Run(() => sendCommandToSlaves(_infra.slaves, command));
                break;

            case "ping":
                Console.WriteLine("-------------------------------------------");
                Console.WriteLine("pinged");
                break;

            case "replconf":
                res = ReplConfSlave(command);
                break;
            
            default:
                res = "+No Response\r\n";
                break;
        }

        return res;
    }

    public string ReplConfSlave(string[] command)
    {
        string res="";
        switch (command[1])
        {
            case "GETACK":
                res = _parser.RespArray(
                        new string[] {"REPLCONF", "ACK", _config.masterReplOffset.ToString()}
                    );
                break;

            default:
                res = "Invalid options";
                break;
        }

        return res;
    }

    public async Task<ResponseDTO> Handle(string[] command, Client client, DateTime currTime, int bufferSize)
    {

        string cmd = command[0];
        
        string res = "";
        byte[]? data = null;
        switch (cmd)
        {
            case "ping":
                
                res = "+PONG\r\n";
                break;

            case "echo":
                res = $"+{command[1]}\r\n";
                break;

            case "get":
                res = _store.Get(command, currTime);
                break;

            case "set":
                res = Set(client, command);
                _infra.bytesSentToSlave += bufferSize;
                _ = Task.Run(async () => await sendCommandToSlaves(_infra.slaves, command));
                break;

            case "info":
                res = Info(command);
                break;

            case "replconf":
                res = ReplConf(command, client);
                break;

            case "wait":
                res = await WaitAsync(command, client);
                break;

            case "psync":
                ResponseDTO response = Psync(command, client);
                res = response.response;
                data = response.data;
                break;

            default:
                res = "+No Response\r\n";
                break;
        }

        return new ResponseDTO(res,data);
    }

    public string Set(Client client, string[] command)
    {
        
        IPEndPoint remoteEndPoint = client.socket.Client.RemoteEndPoint as IPEndPoint;
        if (_config.role.Equals("slave"))
        {
            string clientIpAddress = remoteEndPoint.Address.ToString();
            int clientPort = remoteEndPoint.Port;

            if (_config.masterHost.Equals(clientIpAddress))
            {
                return _store.Set(command);
            }
            else
            {
                return _parser.RespBulkString("READONLY You can't write against a read only replica.");
            }
        }
        var res = _store.Set(command);

        return res;
    }
    public async Task sendCommandToSlaves(ConcurrentBag<Slave> slaves, string[] command)
    {
        // add support for the use of eof and psync2 capabilities
        foreach (Slave slave in slaves)
        {
            string commandRespString = _parser.RespArray(command);
            
            await slave.connection.SendAsync(commandRespString);
        }
    }

    public async Task<string> WaitAsync(string[] command, Client client)
    {
        int c = 0;
        string[] getackarr = new string[] { "REPLCONF", "GETACK", "*" };
        string getack = _parser.RespArray(getackarr);
        byte[] byteArray = Encoding.UTF8.GetBytes(getack);
        int bufferSize = byteArray.Length;

        int required = int.Parse(command[1]);
        int time = int.Parse(command[2]);

        using (var cts = new CancellationTokenSource(time)) // Timeout in milliseconds
        {
            var tasks = _infra.slaves.Select(slave => Task.Run(async () =>
            {
                try
                {
                    slave.connection.Send(byteArray);

                    byte[] buffer = new byte[client.socket.ReceiveBufferSize];
                    int bytesRead = await slave.connection.stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    string bytesProcessed = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    int idxColon = bytesProcessed.IndexOf(":");
                    int idxNewline = bytesProcessed.IndexOf("\r\n");
                    string resultString = bytesProcessed.Substring(idxColon + 1, idxNewline - idxColon - 1);

                    return int.Parse(resultString);
                }
                catch (OperationCanceledException)
                {
                    return -1; // Return a value indicating failure or cancellation
                }
            }, cts.Token)).ToList();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < time && c < required)
            {
                try
                {
                    Task<int> completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);

                    int result = await completedTask;

                    if (result == _infra.bytesSentToSlave)
                    {
                        c++;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (c < required)
            return _parser.RespInteger(c);
        return _parser.RespInteger(required);
    }

    public string Info(string[] command)
    {
        switch (command[1])
        {
            case "replication":
                try
                {
                    return Replication();
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            default:
                return "Invalid options";

        }
    }
    public string Replication()
    {
        string role = $"role:{_config.role}";
        string masterReplid = $"master_replid:{_config.masterReplId}";
        string masterReplOffset = $"master_repl_offset:{_config.masterReplOffset}";

        string[] info = [role, masterReplid, masterReplOffset];

        string replicationData = string.Join("\r\n", info);

        return _parser.RespBulkString(replicationData);
    }


    public string ReplConf(string[] command, Client client)
    {
        string clientIpAddress = client.remoteIpEndPoint.Address.ToString();
        int clientPort = client.remoteIpEndPoint.Port;

        switch (command[1])
        {
            case "listening-port":
                try
                {
                    Slave s = new Slave(++slaveId, client);
                    _infra.slaves.Add(s);

                    return "+OK\r\n";
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return "+NOTOK\r\n";
                }
            case "capa":
                try
                {
                    Slave slave = _infra.slaves.First((x) => { return x.connection.ipAddress.Equals(clientIpAddress); });
                    for (int i = 0; i < command.Length; i++)
                    {
                        if (command[i].Equals("capa"))
                        {
                            slave.capabilities.Add(command[i + 1]);
                        }
                    }

                    return "+OK\r\n";
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return "+NOTOK\r\n";
                }

        }
        return "+OK\r\n";
    }


    public ResponseDTO Psync(string[] command, Client client)
    {
        // add support for the use of eof and psync2 capabilities
        try
        {
            string clientIpAddress = client.remoteIpEndPoint.Address.ToString();
            int clientPort = client.remoteIpEndPoint.Port;

            string replicationIdMaster = command[1];
            string replicationOffsetMaster = command[2];

            if (replicationIdMaster.Equals("?") && replicationOffsetMaster.Equals("-1"))
            {
                // TODO READ RDB FILE FROM FS
                string emptyRdbFileBase64 =
           "UkVESVMwMDEx+glyZWRpcy12ZXIFNy4yLjD6CnJlZGlzLWJpdHPAQPoFY3RpbWXCbQi8ZfoIdXNlZC1tZW3CsMQQAPoIYW9mLWJhc2XAAP/wbjv+wP9aog==";

                byte[] rdbFile = Convert.FromBase64String(emptyRdbFileBase64);

                byte[] rdbResynchronizationFileMsg =
                    Encoding.ASCII.GetBytes($"${rdbFile.Length}\r\n")
                        .Concat(rdbFile)
                        .ToArray();

                string res = $"+FULLRESYNC {_config.masterReplId} {_config.masterReplOffset}\r\n";
                return new ResponseDTO(res,rdbResynchronizationFileMsg);
            }
            else
            {
                return new ResponseDTO("Options not supported");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return new ResponseDTO("Options not supported");
        }
    }
}