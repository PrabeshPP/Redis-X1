using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new(IPAddress.Any, 6379);
server.Start();

string pongResponse = "+PONG\r\n";
string okResponse = "+OK\r\n";
string bulkString = "$-1\r\n";


Dictionary<string, RedisExpiryModel> strDict = new Dictionary<string, RedisExpiryModel>();


while (true)
{
    Socket socket = await server.AcceptSocketAsync();
    await Task.Run(() => HandleMultipleConnection(socket));
}




async void HandleMultipleConnection(Socket socket)
{
    try
    {
        while (true)
        {
            byte[] buffer = new byte[1024];
            int byteRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
            string req = Encoding.UTF8.GetString(buffer, 0, byteRead);
            List<string> command = RedisReqParser(req);

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
                    DateTime _expiryValue = DateTime.Now.AddMilliseconds(timeOut-1);
                    RedisExpiryModel redisExpiryModel = new RedisExpiryModel(value, _expiryValue);
                    strDict.Add(key, redisExpiryModel);
                }
                else
                {
                    RedisExpiryModel redisExpiryModel = new RedisExpiryModel(value, null);
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
                    Console.WriteLine($"Current Time1:{DateTime.Now}");
                    Console.WriteLine($"Expiry Time1:{redisExpiryModel?.Expiry}");
                    await socket.SendAsync(Encoding.UTF8.GetBytes(bulkString), SocketFlags.None);
                }
                else if (redisExpiryModel?.Expiry == null)
                {
                    Console.WriteLine($"Current Time2:{DateTime.Now}");
                    Console.WriteLine($"Expiry Time2:{redisExpiryModel?.Expiry}");
                    string? cValue = redisExpiryModel?.Value;
                    getStr.Append(cValue?.Length);
                    getStr.Append("\r\n");
                    getStr.Append(cValue);
                    getStr.Append("\r\n");
                    await socket.SendAsync(Encoding.UTF8.GetBytes(getStr.ToString()), SocketFlags.None);
                }
                else if (redisExpiryModel?.Expiry != null && DateTime.Now>redisExpiryModel?.Expiry )
                {
                    Console.WriteLine($"Current Time3:{DateTime.Now}");
                    Console.WriteLine($"Expiry Time3:{redisExpiryModel?.Expiry}");
                    await socket.SendAsync(Encoding.UTF8.GetBytes(bulkString), SocketFlags.None);
                }
                else if(redisExpiryModel?.Expiry!=null && DateTime.Now<redisExpiryModel?.Expiry)
                {
                    Console.WriteLine($"Current Time4:{DateTime.Now}");
                    Console.WriteLine($"Expiry Time4:{redisExpiryModel?.Expiry}");
                    string? cValue = redisExpiryModel?.Value;
                    getStr.Append(cValue?.Length);
                    getStr.Append("\r\n");
                    getStr.Append(cValue);
                    getStr.Append("\r\n");
                    await socket.SendAsync(Encoding.UTF8.GetBytes(getStr.ToString()), SocketFlags.None);
                }

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



List<string> RedisReqParser(string request)
{
    string[] strList = request.Split("\r\n");
    if (strList[0][0] != '*')
    {
        throw new Exception("Error in Encoded Message");
    }

    int length = int.Parse(strList[0][1..]);
    return strList.Skip(1).Take(length * 2).Where(x => !x.StartsWith("$")).ToList();
}

//transforming the redis expiry time
// string Transform(RedisExpiryModel redisExpiryModel){
//     if(redisExpiryModel.Expiry!=null && )
//     return "";
// }


//defining struct for the expiry time
public struct RedisExpiryModel
{
    public string Value { get; set; }
    public DateTime? Expiry { get; set; }

    public RedisExpiryModel(string value, DateTime? expiry)
    {
        Value = value;
        Expiry = expiry;
    }
}

