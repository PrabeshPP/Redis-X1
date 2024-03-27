using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new (IPAddress.Any, 6379);
server.Start();

string responseTxt = "+PONG\r\n";


while(true){
    Socket socket = await server.AcceptSocketAsync();
   await Task.Run(()=>HandleMultipleConnection(socket));
}




async void HandleMultipleConnection(Socket socket){
    try{
        byte[] buffer = new byte[1024];
        int byteRead = await socket.ReceiveAsync(buffer,SocketFlags.None);
        string command = Encoding.UTF8.GetString(buffer,0,byteRead);
        Console.WriteLine(command);
        string[] parts = command.Split(" ");
        string commandName = parts[0].ToUpper();

        if(commandName == "Echo"){
            string arguments = parts.Length>1? parts[1]:" ";
            responseTxt = $"+{arguments.Length}\r\n{arguments}\r\n";
            await socket.SendAsync(Encoding.UTF8.GetBytes(responseTxt),SocketFlags.None);
        }
        socket.Shutdown(SocketShutdown.Both);
        socket.Close();
    }catch(Exception ex){
        Console.WriteLine("Error handling cient: "+ex.Message);
    }
}

