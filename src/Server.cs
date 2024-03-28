using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new (IPAddress.Any, 6379);
server.Start();

string pongResponse = "+PONG\r\n";
string okResponse = "+OK\r\n";

Dictionary<string,string> strDict = new Dictionary<string, string>();


while(true){
    Socket socket = await server.AcceptSocketAsync();
   await Task.Run(()=>HandleMultipleConnection(socket));
}




async void HandleMultipleConnection(Socket socket){
    try{
       while(true){
         byte[] buffer = new byte[1024];
        int byteRead = await socket.ReceiveAsync(buffer,SocketFlags.None);
        string req = Encoding.UTF8.GetString(buffer,0,byteRead);
        List<string> command = RedisReqParser(req);

        if(command.Count == 0){
            await socket.SendAsync(Encoding.UTF8.GetBytes(""),SocketFlags.None);
        }
        string cmd = command[0].ToLower();

        if(cmd == "ping"){
            await socket.SendAsync(Encoding.UTF8.GetBytes(pongResponse),SocketFlags.None);
        }else if(cmd == "echo"){
            string eTxt = $"+{command[1]}\r\n";
            await socket.SendAsync(Encoding.UTF8.GetBytes(eTxt),SocketFlags.None);
        }else if(cmd == "set"){
            string key = command[1];
            string value = command[2];
            strDict.Add(key,value);
            await socket.SendAsync(Encoding.UTF8.GetBytes(okResponse),SocketFlags.None);
        }else if(cmd == "get"){
            string key = command[1];
            string value = strDict[key];
            string eTxt = $"+{value}\r\n";
            await socket.SendAsync(Encoding.UTF8.GetBytes(value),SocketFlags.None);
        }else{
            await socket.SendAsync(Encoding.UTF8.GetBytes("Command Not Found"),SocketFlags.None);
        }
       }
        
    }catch(Exception ex){
        Console.WriteLine("Error handling cient: "+ex.Message);
    }
}



List<string> RedisReqParser(string request){
    string[] strList = request.Split("\r\n");
    if(strList[0][0]!='*'){
        throw new Exception("Error in Encoded Message");
    }

    int length = int.Parse(strList[0][1..]);
    return strList.Skip(1).Take(length*2).Where(x=>!x.StartsWith("$")).ToList();
}

