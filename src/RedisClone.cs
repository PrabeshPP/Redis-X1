using System.Net;
using System.Net.Sockets;
using System.Text;

public class RedisClone
{
    private readonly string pongResponse = "+PONG\r\n";
    private readonly string okResponse = "+OK\r\n";
    private readonly string bulkString = "$-1\r\n";

    private readonly Dictionary<string, RedisExpiryModel> strDict = new Dictionary<string, RedisExpiryModel>();

    private readonly int m_port;
    private readonly string? m_master_host;
    private readonly int? m_master_port;

    private string? Role => m_master_host == null ? "master" : "slave";



    public RedisClone(string[] args)
    {
        m_port = GetPortOrDefault(args);
        (string? host, int? port) = GetReplicaOf(args);
        if (host is not null && port.HasValue)
        {
            m_master_host = host;
            m_master_port = port;
        }
    }

    public async Task Run()
    {
        TcpListener server = new(IPAddress.Any, m_port);
        server.Start();
        while (true)
        {
            Socket socket = await server.AcceptSocketAsync();
            await Task.Run(() => HandleMultipleConnection(socket));
        }
    }

    private int GetPortOrDefault(string[] args)
    {
        if (args.Length == 2 && args[0].ToLower() == "--port")
        {
            return int.Parse(args[1]);
        }

        return 6379;
    }


    private (string? host, int? port) GetReplicaOf(string[] args)
    {
        if (args.Length > 2 && args[2].ToLower() == "--replicaof")
        {
            return (args[3], int.Parse(args[4]));
        }
        return (null, null);
    }



    public async void HandleMultipleConnection(Socket socket)
    {
        try
        {
            while (true)
            {
                byte[] buffer = new byte[1024];
                int byteRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
                string req = Encoding.UTF8.GetString(buffer, 0, byteRead);
                List<string> command = RedisParser.RedisReqParser(req);

                if (command.Count == 0)
                {
                    await socket.SendAsync(Encoding.UTF8.GetBytes(""), SocketFlags.None);
                }
                string cmd = command[0].ToLower();

                if (cmd == "ping")
                {
                    await socket.SendAsync(Encoding.UTF8.GetBytes(pongResponse), SocketFlags.None);
                }
                else if (cmd == "echo")
                {
                    string eTxt = $"+{command[1]}\r\n";
                    await socket.SendAsync(Encoding.UTF8.GetBytes(eTxt), SocketFlags.None);
                }
                else if (cmd == "set")
                {
                    string key = command[1];
                    string value = command[2];

                    if (command.Count > 3)
                    {
                        int timeOut = int.Parse(command[4]);
                        DateTime _expiryValue = DateTime.Now.AddMilliseconds(timeOut - 1);
                        RedisExpiryModel redisExpiryModel = new(value, _expiryValue);
                        strDict.Add(key, redisExpiryModel);
                    }
                    else
                    {
                        RedisExpiryModel redisExpiryModel = new(value, null);
                        strDict.Add(key, redisExpiryModel);
                    }

                    await socket.SendAsync(Encoding.UTF8.GetBytes(okResponse), SocketFlags.None);
                }
                else if (cmd == "get")
                {
                    StringBuilder getStr = new StringBuilder("$");
                    string key = command[1];
                    RedisExpiryModel? redisExpiryModel = strDict[key];
                    if (redisExpiryModel == null)
                    {
                        await socket.SendAsync(Encoding.UTF8.GetBytes(bulkString), SocketFlags.None);
                    }
                    else if (redisExpiryModel?.Expiry == null)
                    {
                        string? cValue = redisExpiryModel?.Value;
                        getStr.Append(cValue?.Length);
                        getStr.Append("\r\n");
                        getStr.Append(cValue);
                        getStr.Append("\r\n");
                        await socket.SendAsync(Encoding.UTF8.GetBytes(getStr.ToString()), SocketFlags.None);
                    }
                    else if (redisExpiryModel?.Expiry != null && DateTime.Now > redisExpiryModel?.Expiry)
                    {
                        await socket.SendAsync(Encoding.UTF8.GetBytes(bulkString), SocketFlags.None);
                    }
                    else if (redisExpiryModel?.Expiry != null && DateTime.Now < redisExpiryModel?.Expiry)
                    {
                        string? cValue = redisExpiryModel?.Value;
                        getStr.Append(cValue?.Length);
                        getStr.Append("\r\n");
                        getStr.Append(cValue);
                        getStr.Append("\r\n");
                        await socket.SendAsync(Encoding.UTF8.GetBytes(getStr.ToString()), SocketFlags.None);
                    }

                }
                else if (cmd == "info")
                {
                    string infoStr = $"$11\r\nrole:{Role}\r\n";
                    await socket.SendAsync(Encoding.UTF8.GetBytes(infoStr), SocketFlags.None);
                }
                else
                {
                    await socket.SendAsync(Encoding.UTF8.GetBytes("Command Not Found"), SocketFlags.None);
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine("Error handling cient: " + ex.Message);
        }
    }


}