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

    public async void Run()
    {
        if (m_master_host != null && m_master_port.HasValue)
        {
            TcpClient master = new TcpClient(m_master_host, m_master_port.Value);
            await using var stream = master.GetStream();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("*1\r\n$4\r\nping\r\n");

            string listeningPortCommand = "*3\r\n$8\r\nREPLCONF\r\n$14\r\nlistening-port\r\n$" + m_port.ToString().Length + "\r\n" + m_port + "\r\n";
            string capabilityCommand = "*3\r\n$8\r\nREPLCONF\r\n$4\r\ncapa\r\n$6\r\npsync2\r\n";

            // Step 2: Send REPLCONF commands
            await writer.WriteAsync(listeningPortCommand);
            await writer.WriteAsync(capabilityCommand);

            // Step 3: Handle Master's Response
            byte[] responseBuffer = new byte[1024];
            await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
            string response = Encoding.UTF8.GetString(responseBuffer);
            if (response.Trim() == "+OK\r\n+OK\r\n")
            {
                Console.WriteLine("Handshake completed successfully.");
            }
            else
            {
                Console.WriteLine("Handshake failed. Master's response: " + response);
            }
        }
        TcpListener server = new(IPAddress.Any, m_port);
        try
        {
            server.Start();
            while (true)
            {
                Socket socket = server.AcceptSocket(); // wait for client
                Thread newThread = new Thread(() => HandleMultipleConnection(socket));
                newThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception! Message - ${ex.Message}");
        }
        finally
        {
            server.Stop();
        }
    }

    private int GetPortOrDefault(string[] args)
    {
        var i = Array.FindIndex(
        args, 0, s => s.Equals("--port", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 <= args.Length)
        {
            if (int.TryParse(args[i + 1], out var o))
            {
                return o;
            }
        }
        return 6379;
    }


    private (string? host, int? port) GetReplicaOf(string[] args)
    {
        var i = Array.FindIndex(
        args, 0,
        s => s.Equals("--replicaof", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 2 <= args.Length)
        {
            if (int.TryParse(args[i + 2], out var o))
            {
                return (args[i + 1], o);
            }
        }
        return (null, null);
    }



    public async void HandleMultipleConnection(Socket socket)
    {
        try
        {
            while (socket.Connected)
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
                    Dictionary<string, string> values = new Dictionary<string, string>() {
                                { "role", Role },
                                { "master_replid", "8371b4fb1155b71f4a04d3e1bc3e18c4a990aeeb" },
                                { "master_repl_offset", "0" },};
                    string infoStr = string.Join('\n', values.Select(x => $"{x.Key}:{x.Value}"));
                    await socket.SendAsync(Encoding.UTF8.GetBytes($"${infoStr.Length}\r\n{infoStr}\r\n"), SocketFlags.None);
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