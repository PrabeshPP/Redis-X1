using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new (IPAddress.Any, 6379);
server.Start();

string responseTxt = "+PONG\r\n";
byte[] responseData = Encoding.UTF8.GetBytes(responseTxt);

while(true){
    Socket socket = server.AcceptSocket();
   await Task.Run(()=>HandleMultipleConnection(socket));
}




async void HandleMultipleConnection(Socket socket){
    while(socket.Connected){
    byte[] buffer = new byte[1024];
    await socket.ReceiveAsync(buffer,SocketFlags.None);
    await socket.SendAsync(responseData,SocketFlags.None);
    }
}

